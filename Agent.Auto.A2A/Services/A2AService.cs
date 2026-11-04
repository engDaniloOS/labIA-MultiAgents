using System.Text.Json;
using Agent.Auto.A2A.Agents;
using Agent.Auto.A2A.Models;

namespace Agent.Auto.A2A.Services;

public partial class A2AService(IMessageAgent agent, ConversationStore store, ILogger<A2AService> logger)
{
    private const int InvalidRequest = -32600;
    private const int MethodNotFound = -32601;
    private const int InvalidParams = -32602;
    private const int InternalError = -32603;

    private const string RoleAgent = "ROLE_AGENT";

    public async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Jsonrpc != "2.0" || string.IsNullOrWhiteSpace(request.Method))
            return Error(request.Id, InvalidRequest, "Request payload validation error");

        return request.Method switch
        {
            "SendMessage" => await SendMessageAsync(request, cancellationToken),
            _ => Error(request.Id, MethodNotFound, "Method not found")
        };
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

        string reply;

        try
        {
            reply = await agent.RespondAsync(history, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao gerar a resposta do agente (contextId {ContextId})", contextId);
            return Error(request.Id, InternalError, "Internal error");
        }

        store.AddAssistantMessage(contextId, reply);

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
