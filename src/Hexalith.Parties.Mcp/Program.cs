using Hexalith.Parties.Mcp;
using Hexalith.Parties.ServiceDefaults;

using ModelContextProtocol.Server;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

_ = builder.AddServiceDefaults();

_ = builder.Services
    .AddOptions<PartiesMcpOptions>()
    .Bind(builder.Configuration.GetSection(PartiesMcpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

_ = builder.Services.AddHttpContextAccessor();

_ = builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

WebApplication app = builder.Build();

_ = app.MapMcp();
_ = app.MapDefaultEndpoints();

app.Run();

public partial class Program;
