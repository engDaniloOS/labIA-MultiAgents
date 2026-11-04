using Agent.Auto.Web.Components;
using Agent.Auto.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// A chamada ao orquestrador parte do servidor Blazor, então não há CORS nem chave exposta ao navegador.
builder.Services.AddHttpClient<OrchestratorClient>(client =>
{
    var baseUrl = builder.Configuration["Orchestrator:BaseUrl"]
        ?? throw new InvalidOperationException("Defina Orchestrator:BaseUrl (ou a variável Orchestrator__BaseUrl).");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(3);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
