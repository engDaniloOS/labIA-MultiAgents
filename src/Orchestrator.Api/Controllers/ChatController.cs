using Agent.Auto.Orchestor.Models;
using Agent.Auto.Orchestor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Auto.Orchestor.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController(AgentRegistry registry, AgentClient client, ILogger<ChatController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post(ChatRequest request, CancellationToken cancellationToken)
    {
        var agent = await registry.FindAsync(request.Agent, cancellationToken);

        if (agent is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: $"Agente '{request.Agent}' não existe.");

        if (!agent.Available)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: $"Agente '{agent.Key}' está indisponível.");

        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId;

        try
        {
            var reply = await client.SendMessageAsync(agent, request.Message, conversationId, cancellationToken);

            return Ok(new ChatResponse(conversationId, agent.Key, reply));
        }
        catch (AgentCallException ex)
        {
            logger.LogWarning(ex, "Falha ao chamar o agente {Agent}", agent.Key);
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "O agente não conseguiu responder.", detail: ex.Message);
        }
    }
}
