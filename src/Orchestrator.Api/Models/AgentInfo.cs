namespace Agent.Auto.Orchestor.Models;

public sealed record AgentInfo(string Key, string BaseUrl, bool Available, AgentCard? Card, string? Error);
