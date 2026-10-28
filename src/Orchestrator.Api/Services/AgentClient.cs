using System.Text.Json;
using Agent.Auto.Orchestor.Configs;
using Agent.Auto.Orchestor.Models;

namespace Agent.Auto.Orchestor.Services;

public class AgentCallException(string message, Exception? inner = null) : Exception(message, inner);

public class AgentClient(IHttpClientFactory httpClientFactory)
{
    private const string JsonRpcBinding = "JSONRPC";
    private const string RoleUser = "ROLE_USER";
    private const string A2AVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> SendMessageAsync(AgentInfo agent, string text, string contextId, CancellationToken cancellationToken)
    {
        var url = ResolveUrl(agent);

        var message = new A2AMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ContextId = contextId,
            Role = RoleUser,
            Parts = [new A2APart(text)]
        };
        var payload = new JsonRpcRequest("SendMessage", new SendMessageParams(message));

        try
        {
            var client = httpClientFactory.CreateClient(AgentsConfig.CallHttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            request.Headers.Add("A2A-Version", A2AVersion);

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions, cancellationToken)
                ?? throw new AgentCallException($"O agente {agent.Key} devolveu uma resposta vazia.");

            if (body.Error is not null)
                throw new AgentCallException($"O agente {agent.Key} devolveu erro {body.Error.Code}: {body.Error.Message}");

            var reply = string.Join("\n", body.Result?.Message?.Parts.Select(p => p.Text).Where(t => !string.IsNullOrWhiteSpace(t)) ?? []);

            return string.IsNullOrWhiteSpace(reply)
                ? throw new AgentCallException($"O agente {agent.Key} respondeu sem texto.")
                : reply;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (ex is TaskCanceledException && cancellationToken.IsCancellationRequested) throw;
            throw new AgentCallException($"Não foi possível falar com o agente {agent.Key}: {ex.Message}", ex);
        }
    }

    private static Uri ResolveUrl(AgentInfo agent)
    {
        var binding = agent.Card?.SupportedInterfaces
            .FirstOrDefault(i => string.Equals(i.ProtocolBinding, JsonRpcBinding, StringComparison.OrdinalIgnoreCase))
            ?? throw new AgentCallException($"O agente {agent.Key} não anuncia uma interface JSON-RPC.");

        return Uri.TryCreate(binding.Url, UriKind.Absolute, out var uri)
            ? uri
            : throw new AgentCallException($"O agente {agent.Key} anuncia uma URL inválida: {binding.Url}");
    }
}
