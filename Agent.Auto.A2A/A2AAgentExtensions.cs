using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Auto.A2A.Agents;
using Agent.Auto.A2A.Configs;
using Agent.Auto.A2A.Models;
using Agent.Auto.A2A.Services;
using GeminiDotnet;
using GeminiDotnet.Extensions.AI;
using Microsoft.Extensions.AI;

namespace Agent.Auto.A2A;

/// <summary>
/// Tudo o que um serviço de agente A2A precisa, em duas chamadas: <see cref="AddA2AAgent{TAgent}"/> no
/// registro de serviços e <see cref="MapA2AAgent"/> nas rotas.
/// </summary>
public static class A2AAgentExtensions
{
    /// <summary>
    /// Registra o Agent Card (seção "Agent" do appsettings), o cliente do Gemini (variável AI_API_KEY),
    /// o histórico por conversa e o serviço JSON-RPC, usando <typeparamref name="TAgent"/> para responder.
    /// </summary>
    public static IServiceCollection AddA2AAgent<TAgent>(this IServiceCollection services, IConfiguration configuration)
        where TAgent : class, IMessageAgent
    {
        services.ConfigureAgentCard(configuration);

        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

        // A chave só é exigida quando o modelo é de fato usado: o Agent Card continua servindo sem ela.
        // PublicationOnly não guarda a exceção: com a chave corrigida, a próxima chamada já funciona.
        services.AddSingleton(_ => new Lazy<IChatClient>(() =>
        {
            // No compose, ${AI_API_KEY:-} chega como texto vazio quando a variável não está definida.
            var apiKey = configuration["AI_API_KEY"];

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Defina a variável de ambiente AI_API_KEY com a chave da API do Google Gemini.");

            return new GeminiChatClient(new GeminiClientOptions
            {
                ApiKey = apiKey,
                ModelId = configuration["Gemini:ModelId"] ?? "gemini-2.5-flash",
            });
        }, LazyThreadSafetyMode.PublicationOnly));

        services.AddSingleton<IMessageAgent, TAgent>();
        services.AddSingleton<ConversationStore>();
        services.AddSingleton<A2AService>();

        return services;
    }

    /// <summary>
    /// Expõe o Agent Card em /.well-known/agent-card.json e o endpoint JSON-RPC em POST /, que é a URL
    /// anunciada em supportedInterfaces do cartão.
    /// </summary>
    public static IEndpointRouteBuilder MapA2AAgent(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/.well-known/agent-card.json",
            (AgentCardService service) => Results.Ok(service.GetAgentCard()));

        app.MapPost("/", async (HttpRequest httpRequest, A2AService a2a, CancellationToken cancellationToken) =>
        {
            if (!httpRequest.HasJsonContentType())
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

            JsonRpcRequest? request;
            try
            {
                request = await httpRequest.ReadFromJsonAsync<JsonRpcRequest>(cancellationToken);
            }
            catch (JsonException)
            {
                return Results.Ok(new JsonRpcResponse(null, Error: new JsonRpcError(-32700, "Invalid JSON payload")));
            }

            if (request is null)
                return Results.Ok(new JsonRpcResponse(null, Error: new JsonRpcError(-32600, "Request payload validation error")));

            // SendStreamingMessage responde com text/event-stream; os demais métodos, com um JSON-RPC comum.
            if (request.Method == "SendStreamingMessage")
            {
                var stream = a2a.StartStreaming(request, cancellationToken);

                return stream.Events is null
                    ? Results.Ok(stream.Error)
                    : TypedResults.ServerSentEvents(stream.Events);
            }

            return Results.Ok(await a2a.HandleAsync(request, cancellationToken));
        });

        return app;
    }
}
