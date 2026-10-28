using System.ComponentModel.DataAnnotations;

namespace Agent.Auto.Orchestor.Models;

public sealed record ChatRequest
{
    [Required] public string Agent { get; init; } = string.Empty;
    [Required] public string Message { get; init; } = string.Empty;

    public string? ConversationId { get; init; }
}

public sealed record ChatResponse(string ConversationId, string Agent, string Reply);
