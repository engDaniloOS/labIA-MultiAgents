using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
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

    /// <summary>
    /// SendStreamingMessage: devolve o texto da resposta em pedaços, na ordem em que o agente os gera.
    /// Termina quando a Task do agente conclui; falha com <see cref="AgentCallException"/> se ela falhar.
    /// </summary>
    public async IAsyncEnumerable<string> StreamMessageAsync(
        AgentInfo agent,
        string text,
        string contextId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var url = ResolveUrl(agent);

        var message = new A2AMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ContextId = contextId,
            Role = RoleUser,
            Parts = [new A2APart(text)]
        };
        var payload = new JsonRpcRequest("SendStreamingMessage", new SendMessageParams(message));

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.Add("A2A-Version", A2AVersion);

        HttpResponseMessage response;
        try
        {
            var client = httpClientFactory.CreateClient(AgentsConfig.CallHttpClientName);

            // ResponseHeadersRead: devolve assim que os cabeçalhos chegam, sem esperar o corpo inteiro.
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new AgentCallException($"Não foi possível falar com o agente {agent.Key}: {ex.Message}", ex);
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();

            // Erros de validação antes do início do stream voltam como JSON-RPC comum, não como SSE.
            if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
            {
                var error = await response.Content.ReadFromJsonAsync<JsonRpcResponse>(JsonOptions, cancellationToken);
                throw new AgentCallException(error?.Error is { } e
                    ? $"O agente {agent.Key} devolveu erro {e.Code}: {e.Message}"
                    : $"O agente {agent.Key} não respondeu em streaming.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var events = SseParser.Create(stream, (_, data) => Encoding.UTF8.GetString(data))
                .EnumerateAsync(cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            await using (events)
            {
                while (true)
                {
                    StreamEnvelope? envelope;

                    // yield não pode ficar num try com catch: a leitura do próximo evento é isolada aqui.
                    try
                    {
                        if (!await events.MoveNextAsync())
                            break;

                        envelope = JsonSerializer.Deserialize<StreamEnvelope>(events.Current.Data, JsonOptions);
                    }
                    catch (Exception ex) when (ex is HttpIOException or IOException or JsonException)
                    {
                        throw new AgentCallException($"A conexão com o agente {agent.Key} foi interrompida: {ex.Message}", ex);
                    }

                    if (envelope is null)
                        continue;

                    if (envelope.Error is not null)
                        throw new AgentCallException($"O agente {agent.Key} devolveu erro {envelope.Error.Code}: {envelope.Error.Message}");

                    foreach (var part in envelope.Result?.ArtifactUpdate?.Artifact.Parts ?? [])
                    {
                        if (!string.IsNullOrEmpty(part.Text))
                            yield return part.Text;
                    }

                    var status = envelope.Result?.StatusUpdate?.Status;
                    if (status is null)
                        continue;

                    if (status.State is "TASK_STATE_FAILED" or "TASK_STATE_REJECTED" or "TASK_STATE_CANCELED")
                    {
                        var reason = status.Message?.Parts.FirstOrDefault()?.Text ?? status.State;
                        throw new AgentCallException($"O agente {agent.Key} não concluiu a resposta: {reason}");
                    }

                    if (status.State == "TASK_STATE_COMPLETED")
                        yield break;
                }
            }
        }
    }

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
