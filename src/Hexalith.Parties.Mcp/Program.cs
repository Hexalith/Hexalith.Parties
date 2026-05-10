using Hexalith.Parties.Mcp;
using Hexalith.Parties.Client;
using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.ServiceDefaults;

using Microsoft.Extensions.Options;

using ModelContextProtocol.Server;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

_ = builder.AddServiceDefaults();

_ = builder.Services
    .AddOptions<PartiesMcpOptions>()
    .Bind(builder.Configuration.GetSection(PartiesMcpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

_ = builder.Services.AddHttpContextAccessor();
_ = builder.Services.AddSingleton<IPartiesMcpRequestContextAccessor, HttpPartiesMcpRequestContextAccessor>();
_ = builder.Services.AddTransient<McpContextForwardingHandler>();

_ = builder.Services.AddHttpClient(PartiesMcpHttpClientNames.EventStoreGateway, (serviceProvider, client) =>
{
    PartiesMcpOptions options = serviceProvider.GetRequiredService<IOptions<PartiesMcpOptions>>().Value;
    client.BaseAddress = options.EventStoreGatewayBaseUrl;
})
.AddHttpMessageHandler<McpContextForwardingHandler>();

_ = builder.Services.AddTransient<IPartiesCommandClient>(serviceProvider =>
{
    PartiesMcpOptions options = serviceProvider.GetRequiredService<IOptions<PartiesMcpOptions>>().Value;
    PartiesMcpRequestContext? context = serviceProvider.GetRequiredService<IPartiesMcpRequestContextAccessor>().Current;
    HttpClient httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(PartiesMcpHttpClientNames.EventStoreGateway);

    return new HttpPartiesCommandClient(
        httpClient,
        Options.Create(new PartiesClientOptions
        {
            BaseUrl = options.EventStoreGatewayBaseUrl.ToString(),
            Tenant = context?.TenantId ?? string.Empty,
        }));
});

_ = builder.Services.AddTransient<IPartiesQueryClient>(serviceProvider =>
{
    PartiesMcpOptions options = serviceProvider.GetRequiredService<IOptions<PartiesMcpOptions>>().Value;
    PartiesMcpRequestContext? context = serviceProvider.GetRequiredService<IPartiesMcpRequestContextAccessor>().Current;
    HttpClient httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(PartiesMcpHttpClientNames.EventStoreGateway);

    return new HttpPartiesQueryClient(
        httpClient,
        Options.Create(new PartiesClientOptions
        {
            BaseUrl = options.EventStoreGatewayBaseUrl.ToString(),
            Tenant = context?.TenantId ?? string.Empty,
        }));
});

_ = builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

WebApplication app = builder.Build();

_ = app.MapMcp();
_ = app.MapDefaultEndpoints();

app.Run();

public partial class Program;
