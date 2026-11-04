namespace Agent.Auto.Web.Models;

// Agentes conhecidos pelo orquestrador (GET /api/agents). Só os campos que a tela usa.

public sealed record AgentDto
{
    public string Key { get; init; } = string.Empty;
    public bool Available { get; init; }
    public AgentCardDto? Card { get; init; }
    public string? Error { get; init; }
}

public sealed record AgentCardDto
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

// Eventos de POST /api/chat/stream: start (quem atende), delta (pedaços de texto), done e error.

public abstract record ChatEvent;

public sealed record StartEvent(string ConversationId, string? Agent, bool Routed, double? Confidence) : ChatEvent;

public sealed record DeltaEvent(string Text) : ChatEvent;

public sealed record DoneEvent : ChatEvent;

public sealed record ErrorEvent(string Message) : ChatEvent;
