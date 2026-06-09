using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.Parties.UI;
using Hexalith.Parties.UI.Components;

using Microsoft.FluentUI.AspNetCore.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ADR-030 — ValidateScopes=true so a Singleton capturing a Scoped service fails at boot
// (not silently leak across tenants). MUST sit on the host builder before service resolution.
builder.Host.UseDefaultServiceProvider(o => o.ValidateScopes = true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

// Quickstart chains AddLocalization + AddHexalithShellLocalization + AddHexalithFrontComposer.
builder.Services.AddHexalithFrontComposerQuickstart(o => o.ScanAssemblies(typeof(Program).Assembly));
builder.Services.AddFrontComposerDevMode(builder.Environment);
builder.Services.AddHexalithDomain<PartiesUiDomainMarker>();

WebApplication app = builder.Build();

app.MapStaticAssets();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
