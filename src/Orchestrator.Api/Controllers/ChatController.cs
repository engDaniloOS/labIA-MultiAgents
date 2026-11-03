using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Auto.Orchestor.Models;
using Agent.Auto.Orchestor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Auto.Orchestor.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController(
    AgentRegistry registry,
    AgentRouter router,
    AgentClient client,
    ConversationStore conversations,
    ILogger<ChatController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions EventJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Quem vai atender a mensagem: o agente indicado pelo chamador ou o escolhido pelo roteador.
    /// Se o roteador não tiver confiança, <see cref="Agent"/> vem nulo e <see cref="Clarification"/> traz a pergunta.
    /// Se algo impedir o atendimento (agente inexistente, fora do ar...), <see cref="Failure"/> traz a resposta de erro.
    /// </summary>
    private sealed record Target(
        AgentInfo? Agent, bool Routed, double? Confidence, string? Clarification, IResult? Failure);

    /// <summary>
    /// Envia uma mensagem a um agente e devolve a resposta completa. Se "agent" vier na requisição, ele é usado
    /// direto; senão o roteador escolhe, ou devolve uma pergunta de esclarecimento quando não tem confiança.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> Post(ChatRequest request, CancellationToken cancellationToken)
    {
        var conversationId = NewOrExistingConversationId(request);
        var target = await ResolveTargetAsync(request, conversationId, cancellationToken);

        if (target.Failure is not null)
            return target.Failure;

        if (target.Agent is null)
            return Results.Ok(new ChatResponse(conversationId, null, target.Clarification!, Routed: true, target.Confidence));

        try
        {
            var reply = await client.SendMessageAsync(target.Agent, request.Message, conversationId, cancellationToken);
            conversations.SetLastAgent(conversationId, target.Agent.Key);

            return Results.Ok(new ChatResponse(conversationId, target.Agent.Key, reply, target.Routed, target.Confidence));
        }
        catch (AgentCallException ex)
        {
            logger.LogWarning(ex, "Falha ao chamar o agente {Agent}", target.Agent.Key);
            return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "O agente não conseguiu responder.", detail: ex.Message);
        }
    }

    /// <summary>
    /// Igual a <see cref="Post"/>, mas a resposta chega aos poucos como Server-Sent Events:
    /// "start" (quem atende), vários "delta" (pedaços do texto) e "done"; ou "error" se algo falhar no meio.
    /// Erros que ocorrem antes de começar (agente inexistente ou fora do ar) voltam como HTTP comum.
    /// </summary>
    [HttpPost("stream")]
    [Produces("text/event-stream")]
    public async Task<IResult> Stream(ChatRequest request, CancellationToken cancellationToken)
    {
        var conversationId = NewOrExistingConversationId(request);
        var target = await ResolveTargetAsync(request, conversationId, cancellationToken);

        if (target.Failure is not null)
            return target.Failure;

        return TypedResults.ServerSentEvents(StreamChatAsync(target, request.Message, conversationId, cancellationToken));
    }

    private async IAsyncEnumerable<SseItem<string>> StreamChatAsync(
        Target target,
        string message,
        string conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return Event("start", new ChatStartEvent(conversationId, target.Agent?.Key, target.Routed, target.Confidence));

        // Sem agente: o roteador não teve confiança, então a "resposta" é a pergunta de esclarecimento.
        if (target.Agent is null)
        {
            yield return Event("delta", new ChatDeltaEvent(target.Clarification!));
            yield return new SseItem<string>("{}", "done");
            yield break;
        }

        await using var chunks = client
            .StreamMessageAsync(target.Agent, message, conversationId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        string? failure = null;

        while (true)
        {
            string chunk;

            // yield não pode ficar num try com catch (nem dentro do catch): a leitura do próximo pedaço
            // é isolada aqui e a falha é só anotada.
            try
            {
                if (!await chunks.MoveNextAsync())
                    break;

                chunk = chunks.Current;
            }
            catch (AgentCallException ex)
            {
                logger.LogWarning(ex, "Falha no streaming do agente {Agent}", target.Agent.Key);
                failure = ex.Message;
                break;
            }

            yield return Event("delta", new ChatDeltaEvent(chunk));
        }

        // O HTTP 200 já foi enviado, então a falha vira um evento "error" no próprio fluxo.
        if (failure is not null)
        {
            yield return Event("error", new ChatErrorEvent(failure));
            yield break;
        }

        conversations.SetLastAgent(conversationId, target.Agent.Key);
        yield return new SseItem<string>("{}", "done");
    }

    private static SseItem<string> Event<T>(string type, T data)
        => new(JsonSerializer.Serialize(data, EventJson), type);

    private static string NewOrExistingConversationId(ChatRequest request)
        => string.IsNullOrWhiteSpace(request.ConversationId) ? Guid.NewGuid().ToString("N") : request.ConversationId;

    private async Task<Target> ResolveTargetAsync(ChatRequest request, string conversationId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Agent))
        {
            var found = await registry.FindAsync(request.Agent, cancellationToken);

            if (found is null)
                return Fail(StatusCodes.Status404NotFound, $"Agente '{request.Agent}' não existe.");

            if (!found.Available)
                return Fail(StatusCodes.Status503ServiceUnavailable, $"Agente '{found.Key}' está indisponível.");

            return new Target(found, Routed: false, Confidence: null, Clarification: null, Failure: null);
        }

        var available = (await registry.GetAgentsAsync(cancellationToken)).Where(a => a.Available).ToList();

        if (available.Count == 0)
            return Fail(StatusCodes.Status503ServiceUnavailable, "Nenhum agente está disponível.");

        try
        {
            var decision = await router.RouteAsync(request.Message, available, conversations.GetLastAgent(conversationId), cancellationToken);

            return new Target(decision.Agent, Routed: true, decision.Confidence, decision.Clarification, Failure: null);
        }
        catch (RoutingException ex)
        {
            return Fail(StatusCodes.Status502BadGateway, "O roteador não conseguiu decidir.", ex.Message);
        }
    }

    private static Target Fail(int statusCode, string title, string? detail = null)
        => new(Agent: null, Routed: false, Confidence: null, Clarification: null,
            Failure: Results.Problem(statusCode: statusCode, title: title, detail: detail));
}
