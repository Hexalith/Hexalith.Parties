using Hexalith.Parties.CommandApi.Extensions;
using Hexalith.Parties.CommandApi.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddParties(builder.Configuration);

WebApplication app = builder.Build();

// Middleware pipeline (order matters)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapActorsHandlers();

app.Run();

/// <summary>
/// Entry point class, made partial for WebApplicationFactory test access.
/// </summary>
public partial class Program;
