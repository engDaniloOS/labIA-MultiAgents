using Agent.Auto.Hunter.Configs;
using Agent.Auto.Hunter.Models;
using Microsoft.Extensions.Options;

namespace Agent.Auto.Hunter.Services;

public class AgentCardService(IOptions<AgentCardOptions> options)
{
    private const string ProtocolVersion = "1.0";
    private const string ProtocolBinding = "JSONRPC";

    private readonly AgentCard _card = Build(options.Value);

    public AgentCard GetAgentCard() => _card;

    private static AgentCard Build(AgentCardOptions o) => new()
    {
        Name = o.Name,
        Description = o.Description,
        Version = o.Version,
        SupportedInterfaces = [new AgentInterface(o.PublicUrl, ProtocolBinding, ProtocolVersion)],
        Capabilities = new AgentCapabilities(Streaming: true),
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain"],
        Skills = o.Skills
    };
}
