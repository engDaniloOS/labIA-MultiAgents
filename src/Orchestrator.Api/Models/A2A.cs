namespace Agent.Auto.Orchestor.Models;

public sealed record JsonRpcRequest(string Method, object Params, int Id = 1)
{
    public string Jsonrpc => "2.0";
}

public sealed record JsonRpcResponse
{
    public JsonRpcError? Error { get; init; }
    public SendMessageResult? Result { get; init; }
}

public sealed record JsonRpcError(int Code, string Message);

public sealed record SendMessageParams(A2AMessage Message);

public sealed record SendMessageResult(A2AMessage? Message);

public sealed record A2AMessage
{
    public string? MessageId { get; init; }
    public string? ContextId { get; init; }
    public string Role { get; init; } = string.Empty;
    public IReadOnlyList<A2APart> Parts { get; init; } = [];
}

public sealed record A2APart(string? Text);

// Eventos do SendStreamingMessage, como o orquestrador os lê (só o que interessa para repassar o texto).

public sealed record StreamEnvelope
{
    public JsonRpcError? Error { get; init; }
    public StreamResult? Result { get; init; }
}

public sealed record StreamResult
{
    public StreamStatusUpdate? StatusUpdate { get; init; }
    public StreamArtifactUpdate? ArtifactUpdate { get; init; }
}

public sealed record StreamStatusUpdate(A2ATaskStatus Status);

public sealed record A2ATaskStatus(string State, A2AMessage? Message);

public sealed record StreamArtifactUpdate(A2AArtifact Artifact);

public sealed record A2AArtifact(IReadOnlyList<A2APart> Parts);
