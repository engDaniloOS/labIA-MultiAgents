using System.Text.Json;

namespace Agent.Auto.Seller.Models;

public sealed record JsonRpcRequest
{
    public string? Jsonrpc { get; init; }
    public JsonElement? Id { get; init; }
    public string? Method { get; init; }
    public JsonElement? Params { get; init; }
}

public sealed record JsonRpcResponse(JsonElement? Id, object? Result = null, JsonRpcError? Error = null)
{
    public string Jsonrpc => "2.0";
}

public sealed record JsonRpcError(int Code, string Message);

public sealed record SendMessageParams
{
    public A2AMessage? Message { get; init; }
}

public sealed record A2AMessage
{
    public string? MessageId { get; init; }
    public string? ContextId { get; init; }
    public string? TaskId { get; init; }
    public string Role { get; init; } = string.Empty;
    public IReadOnlyList<A2APart> Parts { get; init; } = [];
}

public sealed record A2APart
{
    public string? Text { get; init; }
}

public sealed record SendMessageResult(A2AMessage Message);

// --- Streaming (SendStreamingMessage): cada evento SSE carrega exatamente uma destas variantes em "result".

public sealed record StreamResponse
{
    public A2ATask? Task { get; init; }
    public A2AStatusUpdate? StatusUpdate { get; init; }
    public A2AArtifactUpdate? ArtifactUpdate { get; init; }
}

public sealed record A2ATask(string Id, string ContextId, A2ATaskStatus Status);

public sealed record A2ATaskStatus(string State, A2AMessage? Message = null);

public sealed record A2AStatusUpdate(string TaskId, string ContextId, A2ATaskStatus Status);

public sealed record A2AArtifactUpdate(string TaskId, string ContextId, A2AArtifact Artifact, bool? Append, bool? LastChunk);

public sealed record A2AArtifact(string ArtifactId, IReadOnlyList<A2APart> Parts);
