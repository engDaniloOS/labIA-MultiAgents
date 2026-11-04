using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Agent.Auto.Web.Models;

namespace Agent.Auto.Web.Services;

/// <summary>Falha ao falar com o orquestrador, com uma mensagem pronta para mostrar na tela.</summary>
public class OrchestratorException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Cliente do orquestrador (BFA): lista os agentes e conversa com eles por Server-Sent Events.</summary>
public class OrchestratorClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AgentDto>> GetAgentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await http.GetFromJsonAsync<List<AgentDto>>("api/agents", JsonOptions, cancellationToken) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            throw new OrchestratorException($"Não foi possível carregar os agentes: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Envia a mensagem e devolve os eventos conforme chegam. Sem <paramref name="agent"/>, o roteador
    /// do orquestrador escolhe o agente.
    /// </summary>
    public async IAsyncEnumerable<ChatEvent> StreamChatAsync(
        string message,
        string? agent,
        string? conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat/stream")
        {
            Content = JsonContent.Create(new { agent, message, conversationId }, options: JsonOptions)
        };

        HttpResponseMessage response;
        try
        {
            // ResponseHeadersRead: devolve assim que os cabeçalhos chegam, sem esperar o corpo inteiro.
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new OrchestratorException($"Não foi possível falar com o orquestrador: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new OrchestratorException(await ReadProblemAsync(response, cancellationToken));

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var events = SseParser.Create(stream, ParseEvent).EnumerateAsync(cancellationToken);

            await foreach (var item in events)
            {
                if (item.Data is not null)
                    yield return item.Data;
            }
        }
    }

    private static ChatEvent? ParseEvent(string eventType, ReadOnlySpan<byte> data) => eventType switch
    {
        "start" => JsonSerializer.Deserialize<StartEvent>(data, JsonOptions),
        "delta" => JsonSerializer.Deserialize<DeltaEvent>(data, JsonOptions),
        "error" => JsonSerializer.Deserialize<ErrorEvent>(data, JsonOptions),
        "done" => new DoneEvent(),
        _ => null
    };

    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);

            var title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
            var detail = doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;

            return string.Join(" ", new[] { title, detail }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        catch (JsonException)
        {
            return $"O orquestrador respondeu {(int)response.StatusCode}.";
        }
    }
}
