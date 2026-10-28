using Agent.Auto.Orchestor.Configs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureAgents(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
