using Agent.Auto.Orchestor.Models;
using Agent.Auto.Orchestor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Auto.Orchestor.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController(AgentRegistry registry) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentInfo>>> GetAll(CancellationToken cancellationToken)
        => Ok(await registry.GetAgentsAsync(cancellationToken));
}
