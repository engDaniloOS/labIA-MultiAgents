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
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post(ChatRequest request, CancellationToken cancellationToken)
    {
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId;

        AgentInfo agent;
        double? confidence = null;
        var routed = string.IsNullOrWhiteSpace(request.Agent);

        if (routed)
        {
            var available = (await registry.GetAgentsAsync(cancellationToken)).Where(a => a.Available).ToList();

            if (available.Count == 0)
                return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Nenhum agente está disponível.");

            RoutingResult decision;
            try
            {
                decision = await router.RouteAsync(request.Message, available, conversations.GetLastAgent(conversationId), cancellationToken);
            }
            catch (RoutingException ex)
            {
                return Problem(statusCode: StatusCodes.Status502BadGateway, title: "O roteador não conseguiu decidir.", detail: ex.Message);
            }

            if (decision.Agent is null)
                return Ok(new ChatResponse(conversationId, null, decision.Clarification!, Routed: true, decision.Confidence));

            agent = decision.Agent;
            confidence = decision.Confidence;
        }
        else
        {
            var found = await registry.FindAsync(request.Agent!, cancellationToken);

            if (found is null)
                return Problem(statusCode: StatusCodes.Status404NotFound, title: $"Agente '{request.Agent}' não existe.");

            if (!found.Available)
                return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: $"Agente '{found.Key}' está indisponível.");

            agent = found;
        }

        try
        {
            var reply = await client.SendMessageAsync(agent, request.Message, conversationId, cancellationToken);
            conversations.SetLastAgent(conversationId, agent.Key);

            return Ok(new ChatResponse(conversationId, agent.Key, reply, routed, confidence));
        }
        catch (AgentCallException ex)
        {
            logger.LogWarning(ex, "Falha ao chamar o agente {Agent}", agent.Key);
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "O agente não conseguiu responder.", detail: ex.Message);
        }
    }
}
