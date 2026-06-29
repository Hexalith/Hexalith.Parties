using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using FluentValidation;

using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Memories.Client.Rest;
using Hexalith.Parties.Authorization;
using Hexalith.Parties.Authentication;
using Hexalith.Parties.Configuration;
using Hexalith.Parties.Domain;
using Hexalith.Parties.ErrorHandling;
using Hexalith.Parties.HealthChecks;
using Hexalith.Parties.Queries;
using Hexalith.Parties.Validation;
using Hexalith.Parties.Search;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Projections.Strategies;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;
using Hexalith.Tenants.Client.Registration;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Extensions;

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
            options.AddPolicy(PartiesRoles.AdminPolicy, policy =>
                policy.RequireRole(PartiesRoles.AdminRoleNames));

            // Story 1.5 (AR-D3) — server-side Consumer policy, registered alongside Admin (same
            // posture: registered + policy-resolvable, role-claim based). Const/role-names/helper live
            // in ConsumerPolicy so the policy is testable in isolation.
            ConsumerPolicy.Add(options);
        });

        // Claims transformation (tenant extraction from JWT)
        _ = services.AddTransient<IClaimsTransformation, PartiesClaimsTransformation>();

        // EventStore server infrastructure (command routing, actors)
        _ = services.AddTransient<IDomainServiceInvoker, PartyDomainServiceInvoker>();
        _ = services.AddEventStoreServer(configuration);
        _ = services.AddHexalithTenants(options => configuration.GetSection("Tenants").Bind(options));
        _ = services.AddOptions<TenantIntegrationOptions>()
            .Bind(configuration.GetSection(TenantIntegrationOptions.SectionName))
            .ValidateOnStart();
        _ = services.AddSingleton<IValidateOptions<TenantIntegrationOptions>, TenantIntegrationOptionsValidator>();
        _ = services
            .AddHttpClient(DaprTenantsReadinessProbe.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(2));
        _ = services.AddSingleton<ITenantsReadinessProbe, DaprTenantsReadinessProbe>();

        // Projection-side only — NOT for command/query gateway authorization (Story 12.3 AC4).
        // EventStore owns gateway tenant validation/RBAC via ITenantValidator/IRbacValidator.
        // Parties retains ITenantAccessService strictly for projection-side / internal actor-host
        // membership lookups against the local Tenants projection. The fitness test
        // PartiesRequestPath_DoesNotUseTenantAccessServiceOrDenialTranslator pins this boundary
        // by asserting the request-path code paths (Program, Domain invoker, command/query
        // controllers) do not consume this service.
        //
        // Singleton lifetime assumes ITenantProjectionStore is also Singleton (the default
        // InMemoryTenantProjectionStore is). Replacing the projection store with a Scoped
        // implementation creates a captive dependency — any such replacement must register
        // the store as Singleton or change this lifetime to Scoped.
        _ = services.AddSingleton<ITenantAccessService, TenantAccessService>();

        // Story 1.5 (AR-D3) — D3 defense-in-depth self-authorization decision service. Pure/stateless
        // aggregateId == party_id check, fail-closed (deny on null/empty/mismatch). Singleton mirrors
        // ITenantAccessService above (no captive-dependency concern). KEPT OFF THE REQUEST PATH: the
        // parties actor host is machine-to-machine over DAPR at POST /process and carries no end-user
        // principal there (DAPR strips the JWT), so there is no consumer party_id to check on the request
        // path today — the EventStore gateway owns request-path RBAC and the active own-data-only
        // enforcement is the BFF self-scope accessor (Story 1.5 AC1). This is the registered, unit-tested
        // building block the deferred gateway self-principal will consume. The fitness test
        // PartiesRequestPath_DoesNotUseDataSubjectAccessService pins it out of Program.cs and the domain
        // invoker (AC4).
        _ = services.AddSingleton<IDataSubjectAccessService, DataSubjectAccessService>();

        // Single concrete registration so both interfaces resolve to the same instance per scope.
        // Two separate AddTransient<TInterface, TImpl>() calls would create two parallel
        // orchestrators, silently splitting any state or cache between the synchronous-update
        // and poller-delivery paths.
        _ = services.AddTransient<PartyProjectionUpdateOrchestrator>();
        _ = services.AddTransient<IProjectionUpdateOrchestrator>(sp => sp.GetRequiredService<PartyProjectionUpdateOrchestrator>());
        _ = services.AddTransient<IProjectionPollerDeliveryGateway>(sp => sp.GetRequiredService<PartyProjectionUpdateOrchestrator>());
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
        _ = services.AddSingleton<CachedPartyKeyManagementService>(sp =>
            new CachedPartyKeyManagementService(sp.GetRequiredService<PartyKeyManagementService>()));
        _ = services.AddSingleton<IPartyKeyManagementService>(sp => sp.GetRequiredService<CachedPartyKeyManagementService>());
        _ = services.AddSingleton<ITenantKeyRotationCacheInvalidator>(sp => sp.GetRequiredService<CachedPartyKeyManagementService>());
        _ = services.AddSingleton<ITenantKeyRotationService, TenantKeyRotationService>();
        _ = services.AddSingleton<ICryptoStatusProvider>(sp => sp.GetRequiredService<PartyKeyLifecycleService>());
        _ = services.AddSingleton<DecryptionCircuitBreaker>();
        _ = services.AddSingleton<IEventPayloadProtectionService, PartyPayloadProtectionService>();
        _ = services.AddSingleton<IPersonalDataCommandGuard, PartyPersonalDataCommandGuard>();
        _ = services.AddSingleton<IPartyErasureRecordStore, PartyErasureRecordStore>();
        PartyMemorySearchOptions memorySearch = configuration
            .GetSection(PartyMemorySearchOptions.SectionName)
            .Get<PartyMemorySearchOptions>() ?? new PartyMemorySearchOptions();
        _ = services.AddSingleton<IReadOnlyList<ErasureStoreCleanupDelegate>>(sp => {
            IActorProxyFactory actorProxyFactory = sp.GetRequiredService<IActorProxyFactory>();
            List<ErasureStoreCleanupDelegate> cleanups =
            [
                async (tenantId, partyId, cancellationToken) =>
                {
                    IPartyDetailProjectionActor detailProxy = actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
                        new ActorId(PartyActorIds.Detail(tenantId, partyId)),
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
                        new ActorId(PartyActorIds.Index(tenantId)),
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
                (tenantId, partyId, cancellationToken) => Task.FromResult(new ErasureVerificationStoreResult
                {
                    StoreName = "aggregate-readable-state",
                    Status = ErasureStoreCleanupStatus.Cleaned,
                    Timestamp = DateTimeOffset.UtcNow,
                }),
                (tenantId, partyId, cancellationToken) => Task.FromResult(new ErasureVerificationStoreResult
                {
                    StoreName = "snapshots",
                    Status = ErasureStoreCleanupStatus.Cleaned,
                    Timestamp = DateTimeOffset.UtcNow,
                }),
            ];

            if (memorySearch.Enabled)
            {
                cleanups.Add(async (tenantId, partyId, cancellationToken) =>
                {
                    // Read the current options on every call so a runtime config reload
                    // (`IOptionsMonitor`) is honoured. Capturing CaseId at registration time
                    // would silently bind cleanup to whatever value was set at boot.
                    PartyMemorySearchOptions current = sp
                        .GetRequiredService<IOptionsMonitor<PartyMemorySearchOptions>>()
                        .CurrentValue;
                    string memoriesCaseId = current.CaseId ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(memoriesCaseId))
                    {
                        return new ErasureVerificationStoreResult
                        {
                            StoreName = "memories-search",
                            Status = ErasureStoreCleanupStatus.Failed,
                            Timestamp = DateTimeOffset.UtcNow,
                            ErrorMessage = "Memories cleanup blocked: Parties:MemoriesSearch:CaseId is not configured.",
                        };
                    }

                    PartyMemoryCleanupService cleanupService = sp.GetRequiredService<PartyMemoryCleanupService>();
                    PartyMemoryCleanupResult result = await cleanupService
                        .DeleteByPartyAsync(tenantId, memoriesCaseId, partyId, cancellationToken)
                        .ConfigureAwait(false);

                    return new ErasureVerificationStoreResult
                    {
                        StoreName = "memories-search",
                        Status = result.Cleaned ? ErasureStoreCleanupStatus.Cleaned : ErasureStoreCleanupStatus.Failed,
                        Timestamp = DateTimeOffset.UtcNow,
                        ErrorMessage = result.BlockedReason,
                    };
                });
            }
            else
            {
                cleanups.Add((tenantId, partyId, cancellationToken) => Task.FromResult(new ErasureVerificationStoreResult
                {
                    StoreName = "memories-search",
                    Status = ErasureStoreCleanupStatus.NotApplicable,
                    Timestamp = DateTimeOffset.UtcNow,
                }));
            }

            return cleanups;
        });
        _ = services.AddSingleton<IErasureVerificationService, ErasureVerificationService>();
        _ = services.AddSingleton<PartyErasureOrchestrator>();

        // Projection infrastructure (Epic 3)
        _ = services.AddSingleton<IIndexPartitionStrategy, SingleKeyPartitionStrategy>();
        _ = services.AddOptions<Hexalith.Parties.Projections.Configuration.ProjectionOptions>()
            .Bind(configuration.GetSection(Hexalith.Parties.Projections.Configuration.ProjectionOptions.ConfigurationSection))
            .Validate(o => o.BatchSize > 0, "ProjectionOptions.BatchSize must be greater than 0.")
            .Validate(o => o.BatchTimeWindowMs > 0, "ProjectionOptions.BatchTimeWindowMs must be greater than 0.")
            .ValidateOnStart();
        _ = services.AddHttpClient<LocalPartyProjectionPlatformAdapter>(client => {
            string daprPort = configuration["DAPR_HTTP_PORT"] ?? "3500";
            client.BaseAddress = new Uri($"http://127.0.0.1:{daprPort}");
        });
        _ = services.AddTransient<EventStorePartyProjectionPlatformAdapter>();
        _ = services.AddTransient<IPartyProjectionPlatformAdapter>(sp =>
            sp.GetRequiredService<IOptions<Hexalith.Parties.Projections.Configuration.ProjectionOptions>>()
                .Value
                .PlatformAdapterMode == PartyProjectionPlatformAdapterMode.Local
                ? sp.GetRequiredService<LocalPartyProjectionPlatformAdapter>()
                : sp.GetRequiredService<EventStorePartyProjectionPlatformAdapter>());

        services.AddActors(options => {
            options.Actors.RegisterActor<PartyIndexProjectionQueryActor>();
            options.Actors.RegisterActor<PartyDetailProjectionQueryActor>();
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

        // Search provider (local fallback until Hexalith.Memories rich search is configured)
        _ = services.AddSingleton<IPartySearchProvider, LocalFuzzyPartySearchProvider>();
        _ = services.AddSingleton<LocalPartySearchService>();
        _ = services.AddOptions<PartyMemorySearchOptions>()
            .BindConfiguration(PartyMemorySearchOptions.SectionName)
            .ValidateOnStart();
        _ = services.AddSingleton<IValidateOptions<PartyMemorySearchOptions>, PartyMemorySearchOptionsValidator>();

        if (memorySearch.Enabled)
        {
            // Fail fast at startup if endpoint is missing — the validator catches this too,
            // but DI also constructs MemoriesClient and the typed cleanup HttpClient before
            // ValidateOnStart fires in some hosts.
            if (memorySearch.Endpoint is null || !memorySearch.Endpoint.IsAbsoluteUri)
            {
                throw new InvalidOperationException(
                    $"{PartyMemorySearchOptions.SectionName}:Endpoint must be an absolute URI when Memories search is enabled.");
            }

            _ = services.AddMemoriesClient(options =>
            {
                options.Endpoint = memorySearch.Endpoint;
                options.ApiToken = memorySearch.ApiToken;
            });
            // Per-party → memory-unit-id mapping so erasure cleanup can iterate per-unit
            // DELETEs against the existing per-unit Memories endpoint (AC5 resolved
            // decision #2). Backed by Dapr state store; durable across process restarts.
            // P14: state-store component name is operator-configurable.
            _ = services
                .AddOptions<PartyMemoryUnitMappingStoreOptions>()
                .Bind(configuration.GetSection(PartyMemoryUnitMappingStoreOptions.SectionName));
            _ = services.AddSingleton<IPartyMemoryUnitMappingStore, PartyMemoryUnitMappingStore>();
            _ = services.AddSingleton<PartyMemoryIndexingService>();
            _ = services.AddSingleton<IPartySearchService>(sp => new MemoriesPartySearchService(
                sp.GetRequiredService<MemoriesClient>(),
                sp.GetRequiredService<LocalPartySearchService>(),
                sp.GetRequiredService<IOptionsMonitor<PartyMemorySearchOptions>>(),
                sp.GetRequiredService<ILogger<MemoriesPartySearchService>>()));
            string? memoriesApiToken = memorySearch.ApiToken;
            _ = services.AddHttpClient<PartyMemoryCleanupService>((sp, httpClient) =>
            {
                httpClient.BaseAddress = memorySearch.Endpoint;
                PartyMemoryCleanupService.ConfigureAuthorization(
                    httpClient,
                    memoriesApiToken,
                    sp.GetService<ILogger<PartyMemoryCleanupService>>());
            });
        }
        else
        {
            // Local fallback is the only registered IPartySearchService when Memories is disabled.
            _ = services.AddSingleton<IPartySearchService>(sp => sp.GetRequiredService<LocalPartySearchService>());
        }

        // FluentValidation (assembly scanning — no explicit validator registration)
        _ = services.AddValidatorsFromAssemblyContaining<CreatePartyValidator>();

        _ = services.ConfigureHttpJsonOptions(options => PartiesJsonOptions.ApplyTo(options.SerializerOptions));

        return services;
    }
}
