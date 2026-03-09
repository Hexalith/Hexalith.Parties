using System.Text.Json;
using System.Text.Json.Serialization;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using FluentValidation;

using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.CommandApi.Authentication;
using Hexalith.Parties.CommandApi.ErrorHandling;
using Hexalith.Parties.CommandApi.Mcp;
using Hexalith.Parties.CommandApi.Validation;
using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Projections.Strategies;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.CommandApi.Extensions;

public static class PartiesServiceCollectionExtensions {
    public static IServiceCollection AddParties(this IServiceCollection services, IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // ProblemDetails support (RFC 9457)
        _ = services.AddProblemDetails();

        // Exception handlers (order matters — first match wins)
        _ = services.AddExceptionHandler<PartiesValidationExceptionHandler>();
        _ = services.AddExceptionHandler<PartiesGlobalExceptionHandler>();

        _ = services.AddHttpContextAccessor();

        // JWT Bearer Authentication
        _ = services.AddOptions<PartiesAuthenticationOptions>()
            .BindConfiguration("Authentication:JwtBearer")
            .ValidateOnStart();

        _ = services.AddSingleton<IValidateOptions<PartiesAuthenticationOptions>, ValidatePartiesAuthenticationOptions>();
        _ = services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigurePartiesJwtBearerOptions>();

        _ = services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        _ = services.AddAuthorization(options => {
            options.AddPolicy("Admin", policy =>
                policy.RequireRole("admin", "Admin", "administrator", "Administrator"));
        });

        // Claims transformation (tenant extraction from JWT)
        _ = services.AddTransient<IClaimsTransformation, PartiesClaimsTransformation>();

        // EventStore server infrastructure (command routing, actors)
        _ = services.AddEventStoreServer(configuration);
        _ = services.AddOptions<CommandStatusOptions>()
            .BindConfiguration("EventStore:CommandStatus");
        _ = services.AddSingleton<ICommandStatusStore, DaprCommandStatusStore>();

        // GDPR / crypto-shredding infrastructure
        _ = services.AddOptions<CryptoShreddingOptions>()
            .Bind(configuration.GetSection(CryptoShreddingOptions.ConfigurationSection))
            .ValidateOnStart();
        _ = services.AddOptions<CryptoShreddingOptions>()
            .PostConfigure<ILoggerFactory>((options, loggerFactory) => {
                ILogger startupLogger = loggerFactory.CreateLogger("Hexalith.Parties.CryptoShredding");
                startupLogger.LogInformation(
                    "Crypto-shredding configuration: IsEnabled={IsEnabled}, CircuitBreakerThreshold={Threshold}, BreakDuration={Duration}",
                    options.IsEnabled,
                    options.CircuitBreakerFailureThreshold,
                    options.CircuitBreakerBreakDuration);
            });
        _ = services.AddSingleton<ICorrelationContextAccessor, CorrelationContextAccessor>();
        _ = services.AddSingleton<IKeyStorageBackend, LocalDevKeyStorageBackend>();
        _ = services.AddSingleton<IKeyOperationAuditService, KeyOperationAuditService>();
        _ = services.AddSingleton<PartyKeyManagementService>();
        _ = services.AddSingleton<IPartyKeyRetryScheduler, ActorBackedPartyKeyRetryScheduler>();
        _ = services.AddSingleton<PartyKeyLifecycleService>();
        _ = services.AddSingleton<IPartyKeyLifecycleService>(sp => sp.GetRequiredService<PartyKeyLifecycleService>());
        _ = services.AddSingleton<IPartyKeyManagementService>(sp =>
            new CachedPartyKeyManagementService(sp.GetRequiredService<PartyKeyManagementService>()));
        _ = services.AddSingleton<ICryptoStatusProvider>(sp => sp.GetRequiredService<PartyKeyLifecycleService>());
        _ = services.AddSingleton<DecryptionCircuitBreaker>();
        _ = services.AddSingleton<IEventPayloadProtectionService, PartyPayloadProtectionService>();
        _ = services.AddSingleton<IPersonalDataCommandGuard, PartyPersonalDataCommandGuard>();
        _ = services.AddSingleton<IPartyErasureRecordStore, PartyErasureRecordStore>();
        _ = services.AddSingleton<IReadOnlyList<ErasureStoreCleanupDelegate>>(sp => {
            IActorProxyFactory actorProxyFactory = sp.GetRequiredService<IActorProxyFactory>();
            return
            [
                async (tenantId, partyId, cancellationToken) =>
                {
                    IPartyDetailProjectionActor detailProxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                        new ActorId($"{tenantId}:party-detail:{partyId}"),
                        nameof(PartyDetailProjectionActor));

                    await detailProxy.EraseAsync(partyId).ConfigureAwait(false);
                    return new ErasureVerificationStoreResult
                    {
                        StoreName = "detail-projection",
                        Status = ErasureStoreCleanupStatus.Cleaned,
                        Timestamp = DateTimeOffset.UtcNow,
                    };
                },
                async (tenantId, partyId, cancellationToken) =>
                {
                    IPartyIndexProjectionActor indexProxy = actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                        new ActorId($"{tenantId}:party-index"),
                        nameof(PartyIndexProjectionActor));

                    await indexProxy.EraseAsync(partyId).ConfigureAwait(false);
                    return new ErasureVerificationStoreResult
                    {
                        StoreName = "index-projection",
                        Status = ErasureStoreCleanupStatus.Cleaned,
                        Timestamp = DateTimeOffset.UtcNow,
                    };
                },
                (tenantId, partyId, cancellationToken) => Task.FromResult(new ErasureVerificationStoreResult
                {
                    StoreName = "projection-cache",
                    Status = ErasureStoreCleanupStatus.Cleaned,
                    Timestamp = DateTimeOffset.UtcNow,
                }),
            ];
        });
        _ = services.AddSingleton<IErasureVerificationService, ErasureVerificationService>();
        _ = services.AddSingleton<PartyErasureOrchestrator>();

        // OpenAPI document generation
        _ = services.AddOpenApi();

        // Projection infrastructure (Epic 3)
        _ = services.AddSingleton<IIndexPartitionStrategy, SingleKeyPartitionStrategy>();
        _ = services.AddOptions<ProjectionOptions>()
            .Bind(configuration.GetSection(ProjectionOptions.ConfigurationSection))
            .Validate(o => o.BatchSize > 0, "ProjectionOptions.BatchSize must be greater than 0.")
            .Validate(o => o.BatchTimeWindowMs > 0, "ProjectionOptions.BatchTimeWindowMs must be greater than 0.")
            .ValidateOnStart();

        services.AddActors(options => {
            options.Actors.RegisterActor<PartyDetailProjectionActor>();
            options.Actors.RegisterActor<PartyIndexProjectionActor>();
            options.Actors.RegisterActor<PartyKeyRetryActor>();
        });

        // Actor proxy factory for querying projection actors
        _ = services.AddSingleton<IActorProxyFactory>(_ => new ActorProxyFactory());

        // Projection rebuild service (Story 8.3 — D14/D15)
        _ = services.AddHttpClient<IProjectionRebuildService, ProjectionRebuildService>(client => {
            string daprPort = configuration["DAPR_HTTP_PORT"] ?? "3500";
            client.BaseAddress = new Uri($"http://127.0.0.1:{daprPort}");
        });

        // Search provider (pluggable — D2)
        _ = services.AddSingleton<IPartySearchProvider, SemanticPartySearchProvider>();

        // FluentValidation (assembly scanning — no explicit validator registration)
        _ = services.AddValidatorsFromAssemblyContaining<CreatePartyValidator>();

        // JSON serialization: camelCase, ISO 8601, string enums, omit nulls
        _ = services.AddControllers()
            .AddJsonOptions(options => {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        _ = services.ConfigureHttpJsonOptions(options => {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // MCP Server (Model Context Protocol) — AI agent tool interface
#pragma warning disable MCPEXP002 // RunSessionHandler is experimental; required to capture tenant from HttpContext
        _ = services
            .AddMcpServer()
            .WithHttpTransport(options => {
                options.RunSessionHandler = async (httpContext, mcpServer, ct) => {
                    string? tenant = httpContext.User.FindAll("eventstore:tenant")
                        .Select(c => c.Value)
                        .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                    McpSessionContext.Tenant.Value = tenant;
                    try {
                        await mcpServer.RunAsync(ct).ConfigureAwait(false);
                    }
                    finally {
                        McpSessionContext.Tenant.Value = null;
                    }
                };
            })
            .WithToolsFromAssembly();
#pragma warning restore MCPEXP002

        return services;
    }
}
