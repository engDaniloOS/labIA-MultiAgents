using Agent.Auto.Hunter.Configs;
using Agent.Auto.Hunter.Models;
using Agent.Auto.Hunter.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureAgentCard(builder.Configuration);
builder.Services.ConfigureA2A(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet(
    "/.well-known/agent-card.json",
    (AgentCardService service) => Results.Ok(service.GetAgentCard()));

// Endpoint JSON-RPC do A2A: é a URL anunciada em supportedInterfaces do Agent Card.
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

    // SendStreamingMessage responde com text/event-stream; os demais mÃ©todos, com um JSON-RPC comum.
    if (request.Method == "SendStreamingMessage")
    {
        var stream = a2a.StartStreaming(request, cancellationToken);

        return stream.Events is null
            ? Results.Ok(stream.Error)
            : TypedResults.ServerSentEvents(stream.Events);
    }

    return Results.Ok(await a2a.HandleAsync(request, cancellationToken));
});

app.Run();
