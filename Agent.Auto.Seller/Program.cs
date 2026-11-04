using Agent.Auto.A2A;
using Agent.Auto.Seller.Agents;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddA2AAgent<VendedorAgent>(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapA2AAgent();

app.Run();