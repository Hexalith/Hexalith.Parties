using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

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
    {
        _ = builder.ConfigureOpenTelemetry();

        _ = builder.AddDefaultHealthChecks();

        _ = builder.Services.AddServiceDiscovery();

        _ = builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            _ = http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            _ = http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        _ = builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        // Structured JSON console logging: ensures log output is machine-parseable
        // and structured fields from [LoggerMessage] methods appear as named JSON properties.
        _ = builder.Logging.AddJsonConsole(options => options.UseUtcTimestamp = true);

        _ = builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource("Hexalith.Parties.CommandApi")
                    .AddSource("Hexalith.Parties")
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                            && !context.Request.Path.StartsWithSegments(ReadinessEndpointPath)
                    )
                    .AddHttpClientInstrumentation());

        _ = builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        bool useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            _ = builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Placeholder checks removed — real DAPR health checks are registered
        // in Program.cs via AddPartiesDaprHealthChecks() (Story 8.2).
        _ = builder.Services.AddHealthChecks();

        return builder;
    }

    /// <summary>
    /// Writes a detailed JSON health check response for development environments.
    /// </summary>
    internal static Task WriteHealthCheckJsonResponse(HttpContext httpContext, HealthReport healthReport)
    {
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("status", healthReport.Status.ToString());
            writer.WriteStartObject("results");

            foreach (KeyValuePair<string, HealthReportEntry> entry in healthReport.Entries)
            {
                writer.WriteStartObject(entry.Key);
                writer.WriteString("status", entry.Value.Status.ToString());
                writer.WriteString("description", entry.Value.Description);
                writer.WriteString("duration", entry.Value.Duration.ToString());
                writer.WriteStartObject("data");
                foreach (KeyValuePair<string, object> dataEntry in entry.Value.Data)
                {
                    writer.WritePropertyName(dataEntry.Key);
                    System.Text.Json.JsonSerializer.Serialize(
                        writer,
                        dataEntry.Value,
                        dataEntry.Value?.GetType() ?? typeof(object));
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return httpContext.Response.WriteAsync(
            System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Health check status code mapping: Healthy=200, Degraded=200, Unhealthy=503
        var statusCodes = new Dictionary<HealthStatus, int>
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        };

        // All health checks must pass for app to be considered ready to accept traffic after starting
        var healthOptions = new HealthCheckOptions
        {
            ResultStatusCodes = statusCodes,
        };

        // Development: detailed JSON response; Production: default minimal plaintext
        if (app.Environment.IsDevelopment())
        {
            healthOptions.ResponseWriter = WriteHealthCheckJsonResponse;
        }

        _ = app.MapHealthChecks(HealthEndpointPath, healthOptions);

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        _ = app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
            ResultStatusCodes = statusCodes,
        });

        // Only health checks tagged with the "ready" tag must pass for readiness (K8s readiness probe)
        var readinessOptions = new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready"),
            ResultStatusCodes = statusCodes,
        };

        if (app.Environment.IsDevelopment())
        {
            readinessOptions.ResponseWriter = WriteHealthCheckJsonResponse;
        }

        _ = app.MapHealthChecks(ReadinessEndpointPath, readinessOptions);

        return app;
    }
}
