namespace Agent.Auto.Hunter.Models;

public sealed record AgentCard
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Version { get; init; }
    public required IReadOnlyList<AgentInterface> SupportedInterfaces { get; init; }
    public required AgentCapabilities Capabilities { get; init; }
    public required IReadOnlyList<string> DefaultInputModes { get; init; }
    public required IReadOnlyList<string> DefaultOutputModes { get; init; }
    public required IReadOnlyList<AgentSkill> Skills { get; init; }
}

public sealed record AgentInterface(string Url, string ProtocolBinding, string ProtocolVersion);

public sealed record AgentCapabilities(bool Streaming = false, bool PushNotifications = false);

public sealed record AgentSkill
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public IReadOnlyList<string>? Examples { get; init; }
}
