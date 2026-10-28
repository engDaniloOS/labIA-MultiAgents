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
