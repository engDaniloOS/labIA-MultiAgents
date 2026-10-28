using Agent.Auto.Bank.Configs;
using Agent.Auto.Bank.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureAgentCard(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapGet(
    "/.well-known/agent-card.json",
    (AgentCardService service) => Results.Ok(service.GetAgentCard()));

app.Run();
