using System.Collections.Concurrent;
using System.Text.Json;
using Agent.Auto.Orchestor.Configs;
using Agent.Auto.Orchestor.Models;
using Microsoft.Extensions.Options;

namespace Agent.Auto.Orchestor.Services;

public class AgentRegistry(
    IOptions<AgentsOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<AgentRegistry> logger)
{
    private const string WellKnownPath = "/.well-known/agent-card.json";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(300);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, (AgentInfo Info, DateTimeOffset FetchedAt)> _cache = new();

    public async Task<IReadOnlyList<AgentInfo>> GetAgentsAsync(CancellationToken cancellationToken = default)
    {
        var tasks = options.Value.Endpoints.Select(
            e => GetAgentAsync(e.Key, e.Value.BaseUrl, cancellationToken));

        return await Task.WhenAll(tasks);
    }

    public async Task<AgentInfo?> FindAsync(string key, CancellationToken cancellationToken = default)
    {
        var endpoint = options.Value.Endpoints
            .FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

        return endpoint.Key is null ? null : await GetAgentAsync(endpoint.Key, endpoint.Value.BaseUrl, cancellationToken);
    }

    private async Task<AgentInfo> GetAgentAsync(string key, string baseUrl, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key, out var cached) 
            && DateTimeOffset.UtcNow - cached.FetchedAt < CacheDuration)
        {
            return cached.Info;
        }

        var info = await FetchAsync(key, baseUrl, cancellationToken);

        _cache[key] = (info, DateTimeOffset.UtcNow);

        return info;
    }

    private async Task<AgentInfo> FetchAsync(string key, string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(AgentsConfig.HttpClientName);
            var url = new Uri(new Uri(baseUrl), WellKnownPath);

            var card = await client.GetFromJsonAsync<AgentCard>(url, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("O Agent Card veio vazio.");

            return new AgentInfo(key, baseUrl, Available: true, card, Error: null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Agente {Agent} indisponível em {BaseUrl}", key, baseUrl);
            return new AgentInfo(key, baseUrl, Available: false, Card: null, Error: ex.Message);
        }
    }
}
