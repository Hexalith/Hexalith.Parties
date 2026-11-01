using Hexalith.EventStore.Admin.UI;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.Parties.EventStoreAdminUI.TestHost;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
string? directory = builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"];
if (!builder.Environment.IsDevelopment()
    || builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Role"] != "aux-client"
    || string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
{
    throw new InvalidOperationException("The private Admin UI requires an explicit Development auxiliary-client catalog profile.");
}

builder.AddNativeAdminUiServices();
WebApplication app = builder.Build();
app.UsePrivateAuxiliaryAdminUI();
app.MapDefaultEndpoints();
await app.StartSignalRAsync().ConfigureAwait(false);
await app.RunAsync().ConfigureAwait(false);
