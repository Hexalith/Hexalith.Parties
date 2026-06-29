using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

using Hexalith.Commons.ServiceDefaults;

namespace Hexalith.Parties.ServiceDefaults;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string ReadinessEndpointPath = "/ready";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
        => builder.AddHexalithServiceDefaults(ConfigurePartiesDefaults);

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
        => builder.ConfigureHexalithOpenTelemetry(ConfigurePartiesDefaults);

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
        => builder.AddHexalithDefaultHealthChecks(ConfigurePartiesDefaults);

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.MapHexalithDefaultEndpoints(ConfigurePartiesDefaults);
    }

    private static void ConfigurePartiesDefaults(HexalithServiceDefaultsOptions options)
    {
        options.HealthEndpointPath = HealthEndpointPath;
        options.LivenessEndpointPath = AlivenessEndpointPath;
        options.ReadinessEndpointPath = ReadinessEndpointPath;
        options.RegisterDefaultSelfCheck = false;
        options.ActivitySourceNames.Add("Hexalith.Parties");
    }
}
