using System.ComponentModel.DataAnnotations;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using Microsoft.Extensions.AI;

namespace Agent.Auto.Orchestor.Configs;

public class RoutingOptions
{
    public const string SectionName = "Routing";

    [Range(0, 1)] public double MinConfidence { get; set; } = 0.6;
}

public static class RoutingConfig
{
    public static void ConfigureRouting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RoutingOptions>()
            .Bind(configuration.GetSection(RoutingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(_ => new Lazy<IChatClient>(() =>
        {
            var apiKey = configuration["AI_API_KEY"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Defina a variável de ambiente AI_API_KEY com a chave da API do Google Gemini.");

            return new GeminiChatClient(new GeminiClientOptions
            {
                ApiKey = apiKey,
                ModelId = configuration["Gemini:ModelId"] ?? "gemini-2.5-flash",
            });
        }, LazyThreadSafetyMode.PublicationOnly));

        services.AddSingleton<Services.AgentRouter>();
        services.AddSingleton<Services.ConversationStore>();
    }
}
