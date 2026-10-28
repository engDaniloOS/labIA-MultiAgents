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
