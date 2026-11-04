using System.Diagnostics;
using System.Text.Json;
using Agent.Auto.A2A.Agents;
using Agent.Auto.A2A.Configs;
using Agent.Auto.A2A.Models;
using Microsoft.Extensions.Options;

namespace Agent.Auto.A2A.Services;

public partial class A2AService(
    IMessageAgent agent,
    ConversationStore store,
    IOptions<AgentCardOptions> card,
    ILogger<A2AService> logger)
{
    private const int PreviewLength = 80;

    private const int InvalidRequest = -32600;
    private const int MethodNotFound = -32601;
    private const int InvalidParams = -32602;
    private const int InternalError = -32603;

    private const string RoleAgent = "ROLE_AGENT";

    // Nome do agente nas linhas de log, para saber qual deles respondeu ao ler os logs de vários contêineres.
    private string AgentName => card.Value.Name;

    public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Jsonrpc != "2.0" || string.IsNullOrWhiteSpace(request.Method))
        {
            logger.LogWarning("[{Agent}] Requisição JSON-RPC inválida", AgentName);
            return Error(request.Id, InvalidRequest, "Request payload validation error");
        }

        if (request.Method != "SendMessage")
            logger.LogWarning("[{Agent}] Método não suportado: {Method}", AgentName, request.Method);

        return request.Method switch
        {
            "SendMessage" => await SendMessageAsync(request, cancellationToken),
            _ => Error(request.Id, MethodNotFound, "Method not found")
        };
    }

    // Uma linha só, sem quebras e com tamanho limitado, para o log não virar um despejo da conversa.
    private static string Preview(string text)
    {
        var oneLine = text.ReplaceLineEndings(" ").Trim();
        return oneLine.Length <= PreviewLength ? oneLine : oneLine[..PreviewLength] + "…";
    }

    private async Task<JsonRpcResponse> SendMessageAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var message = ReadMessage(request.Params);

        var text = message is null
            ? string.Empty
            : string.Join("\n", message.Parts.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

        if (string.IsNullOrWhiteSpace(text))
            return Error(request.Id, InvalidParams, "Invalid parameters: message with a text part is required");

        var contextId = string.IsNullOrWhiteSpace(message!.ContextId) ? Guid.NewGuid().ToString("N") : message.ContextId;
        var history = store.AddUserMessage(contextId, text);

        logger.LogInformation(
            "[{Agent}] SendMessage recebido: ctx={ContextId} pergunta=\"{Question}\" ({HistoryCount} mensagens no histórico)",
            AgentName, contextId, Preview(text), history.Count);

        var stopwatch = Stopwatch.StartNew();
        string reply;

        try
        {
            reply = await agent.RespondAsync(history, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[{Agent}] SendMessage falhou: ctx={ContextId} após {ElapsedMs} ms",
                AgentName, contextId, stopwatch.ElapsedMilliseconds);
            return Error(request.Id, InternalError, "Internal error");
        }

        store.AddAssistantMessage(contextId, reply);

        logger.LogInformation("[{Agent}] SendMessage respondeu: ctx={ContextId} {Chars} caracteres em {ElapsedMs} ms",
            AgentName, contextId, reply.Length, stopwatch.ElapsedMilliseconds);

        var response = new A2AMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ContextId = contextId,
            Role = RoleAgent,
            Parts = [new A2APart { Text = reply }]
        };

        return new JsonRpcResponse(request.Id, Result: new SendMessageResult(response));
    }

    private static A2AMessage? ReadMessage(JsonElement? parameters)
    {
        if (parameters is not { ValueKind: JsonValueKind.Object } element)
            return null;

        try
        {
            return element.Deserialize<SendMessageParams>(JsonSerializerOptions.Web)?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonRpcResponse Error(JsonElement? id, int code, string message)
        => new(id, Error: new JsonRpcError(code, message));
}
