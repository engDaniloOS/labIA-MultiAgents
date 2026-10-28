using System.ComponentModel.DataAnnotations;
using Agent.Auto.Bank.Models;

namespace Agent.Auto.Bank.Configs;

public class AgentCardOptions
{
    public const string SectionName = "Agent";

    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public string Version { get; set; } = "1.0.0";
    [Required, Url] public string PublicUrl { get; set; } = string.Empty;
    [MinLength(1)] public List<AgentSkill> Skills { get; set; } = [];
}

public static class AgentCardConfig
{
    public static void ConfigureAgentCard(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentCardOptions>()
            .Bind(configuration.GetSection(AgentCardOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<Services.AgentCardService>();
    }
}
