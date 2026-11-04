using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Auto.A2A.Models;
using Microsoft.Extensions.AI;

namespace Agent.Auto.A2A.Services;

/// <summary>Resultado de iniciar um SendStreamingMessage: ou um erro JSON-RPC comum, ou o fluxo de eventos já serializados.</summary>
public sealed record StreamStart(JsonRpcResponse? Error, IAsyncEnumerable<string>? Events);

/// <summary>
/// SendStreamingMessage do A2A 1.0. O conteúdo incremental segue o modelo da spec: uma Task em andamento,
/// atualizações de artefato (append / lastChunk) com os pedaços do texto e, por fim, o status da Task.
/// Cada evento é uma resposta JSON-RPC completa, enviada como uma linha "data:" do SSE.
/// </summary>
public partial class A2AService
{
    private const string StateWorking = "TASK_STATE_WORKING";
    private const string StateCompleted = "TASK_STATE_COMPLETED";
    private const string StateFailed = "TASK_STATE_FAILED";
    private const string ReplyArtifactId = "reply";

    private static readonly JsonSerializerOptions EventJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public StreamStart StartStreaming(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (request.Jsonrpc != "2.0")
            return new StreamStart(Error(request.Id, InvalidRequest, "Request payload validation error"), null);

        var message = ReadMessage(request.Params);
        var text = message is null
            ? string.Empty
            : string.Join("\n", message.Parts.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

        if (string.IsNullOrWhiteSpace(text))
            return new StreamStart(Error(request.Id, InvalidParams, "Invalid parameters: message with a text part is required"), null);

        var contextId = string.IsNullOrWhiteSpace(message!.ContextId) ? Guid.NewGuid().ToString("N") : message.ContextId;
        var history = store.AddUserMessage(contextId, text);

        return new StreamStart(null, StreamEventsAsync(request.Id, contextId, history, cancellationToken));
    }

    private async IAsyncEnumerable<string> StreamEventsAsync(
        JsonElement? id,
        string contextId,
        IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var taskId = Guid.NewGuid().ToString("N");

        yield return Serialize(id, new StreamResponse
        {
            Task = new A2ATask(taskId, contextId, new A2ATaskStatus(StateWorking))
        });

        var reply = new StringBuilder();
        var failed = false;

        // Segura o pedaço mais recente até chegar o próximo, para poder marcar o último com lastChunk
        // sem precisar enviar um artefato vazio no fim.
        string? pending = null;

        await using var chunks = agent.RespondStreamingAsync(history, cancellationToken).GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            string chunk;

            // yield não pode ficar dentro de um try com catch: a leitura do próximo pedaço é isolada aqui.
            try
            {
                if (!await chunks.MoveNextAsync())
                    break;

                chunk = chunks.Current;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao gerar a resposta em streaming (contextId {ContextId})", contextId);
                failed = true;
                break;
            }

            if (string.IsNullOrEmpty(chunk))
                continue;

            if (pending is not null)
            {
                yield return Serialize(id, ArtifactUpdate(taskId, contextId, pending, append: reply.Length > 0, lastChunk: false));
                reply.Append(pending);
            }

            pending = chunk;
        }

        if (failed || pending is null)
        {
            yield return Serialize(id, StatusUpdate(taskId, contextId, StateFailed, failed ? "Internal error" : "O agente respondeu sem texto."));
            yield break;
        }

        yield return Serialize(id, ArtifactUpdate(taskId, contextId, pending, append: reply.Length > 0, lastChunk: true));
        reply.Append(pending);

        store.AddAssistantMessage(contextId, reply.ToString());

        yield return Serialize(id, StatusUpdate(taskId, contextId, StateCompleted));
    }

    private static StreamResponse ArtifactUpdate(string taskId, string contextId, string text, bool append, bool lastChunk) => new()
    {
        ArtifactUpdate = new A2AArtifactUpdate(
            taskId,
            contextId,
            new A2AArtifact(ReplyArtifactId, [new A2APart { Text = text }]),
            append,
            lastChunk)
    };

    private static StreamResponse StatusUpdate(string taskId, string contextId, string state, string? errorText = null) => new()
    {
        StatusUpdate = new A2AStatusUpdate(
            taskId,
            contextId,
            new A2ATaskStatus(state, errorText is null
                ? null
                : new A2AMessage
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    ContextId = contextId,
                    Role = RoleAgent,
                    Parts = [new A2APart { Text = errorText }]
                }))
    };

    private static string Serialize(JsonElement? id, StreamResponse result)
        => JsonSerializer.Serialize(new JsonRpcResponse(id, Result: result), EventJson);
}
