using System.ComponentModel.DataAnnotations;

namespace Agent.Auto.Orchestor.Models;

public sealed record ChatRequest
{
    public string? Agent { get; init; }

    [Required] public string Message { get; init; } = string.Empty;

    public string? ConversationId { get; init; }
}

/// <param name="Agent">Quem respondeu. Nulo quando o roteador não teve confiança e devolveu uma pergunta de esclarecimento.</param>
/// <param name="Routed">True se o agente foi escolhido (ou recusado) pelo roteador, false se o chamador indicou o agente.</param>
/// <param name="Confidence">Confiança do roteador, quando ele foi usado.</param>
public sealed record ChatResponse(string ConversationId, string? Agent, string Reply, bool Routed, double? Confidence);

// Eventos SSE de POST /api/chat/stream: "start" (quem atende), "delta" (pedaços de texto), "done" e "error".
public sealed record ChatStartEvent(string ConversationId, string? Agent, bool Routed, double? Confidence);

public sealed record ChatDeltaEvent(string Text);

public sealed record ChatErrorEvent(string Message);
