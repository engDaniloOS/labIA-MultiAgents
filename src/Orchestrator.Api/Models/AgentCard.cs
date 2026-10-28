namespace Agent.Auto.Orchestor.Models;

public sealed record AgentCard
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public IReadOnlyList<AgentInterface> SupportedInterfaces { get; init; } = [];
    public IReadOnlyList<AgentSkill> Skills { get; init; } = [];
}

public sealed record AgentInterface(string Url, string ProtocolBinding, string ProtocolVersion);

public sealed record AgentSkill
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string>? Examples { get; init; }
}
