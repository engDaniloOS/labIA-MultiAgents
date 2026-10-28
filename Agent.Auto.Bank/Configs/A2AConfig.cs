using Agent.Auto.Bank.Agents;
using Agent.Auto.Bank.Services;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using Microsoft.Extensions.AI;

namespace Agent.Auto.Bank.Configs;

public static class A2AConfig
{
    public static void ConfigureA2A(this IServiceCollection services, IConfiguration configuration)
    {
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

        services.AddSingleton<IMessageAgent, FinanciamentoAgent>();
        services.AddSingleton<ConversationStore>();
        services.AddSingleton<A2AService>();
    }
}
