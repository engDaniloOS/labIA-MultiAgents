using System.ComponentModel.DataAnnotations;

namespace Agent.Auto.Orchestor.Configs;

public class AgentsOptions
{
    public const string SectionName = "Agents";

    [MinLength(1)] public Dictionary<string, AgentEndpointOptions> Endpoints { get; set; } = [];
}

public class AgentEndpointOptions
{
    [Required, Url] public string BaseUrl { get; set; } = string.Empty;
}

public static class AgentsConfig
{
    public const string HttpClientName = "agents";
    public const string CallHttpClientName = "agent-calls";

    public static void ConfigureAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentsOptions>()
            .Configure(o => configuration.GetSection(AgentsOptions.SectionName).Bind(o.Endpoints))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddHttpClient(CallHttpClientName, c => c.Timeout = TimeSpan.FromSeconds(90));

        services.AddSingleton<Services.AgentRegistry>();
        services.AddSingleton<Services.AgentClient>();
    }
}
