using Dapr.Actors.Client;
using Dapr.Client;

using FluentValidation;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.DomainService;
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
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Search;
using Hexalith.Parties.Projections.Services;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;
using Hexalith.Tenants.Client.Registration;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Extensions;

public static class PartiesServiceCollectionExtensions {
    private const string PartyDomain = "party";

    public static IServiceCollection AddParties(this IServiceCollection services, IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(configuration);
        // PartySdkQueryService (and other seams) require TimeProvider; the generic host does not
        // register it by default. Match Tenants / Parties.UI and keep TryAdd so hosts may override.
        services.TryAddSingleton(TimeProvider.System);

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

        // EventStore domain-service SDK invokes this keyed processor for POST /process.
        // Keep the Parties-specific compatibility behavior here until EventStore owns
        // validation, protected-state redaction, and erasure-status hooks.
        _ = services.AddKeyedScoped<IDomainProcessor, PartyDomainProcessor>(PartyDomain);
        foreach (string domainKey in PartyDomainCaseVariants())
        {
            if (!string.Equals(domainKey, PartyDomain, StringComparison.Ordinal))
            {
                _ = services.AddKeyedScoped<IDomainProcessor, PartyDomainProcessor>(domainKey);
            }
        }

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
        // by asserting the request-path code paths (Program, domain processor, command/query
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
        // processor (AC4).
        _ = services.AddSingleton<IDataSubjectAccessService, DataSubjectAccessService>();

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
        _ = services.AddSingleton<IPartyErasureRecordStore, PartyErasureRecordStore>();
        _ = services.AddEventStoreReadModelStore();
        _ = services.AddEventStoreDataProtection(configuration, "Hexalith.Parties");
        _ = services.AddEventStoreQueryCursorCodec("Hexalith.Parties.QueryCursor.v1");
        _ = services.AddOptions<PartySdkReadModelOptions>()
            .Bind(configuration.GetSection(PartySdkReadModelOptions.ConfigurationSection))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ReadModelStateStoreName),
                "Party SDK read-model state store name must not be empty.")
            .Validate(options => options.FreshnessAgingSeconds >= 0
                    && options.FreshnessStaleSeconds >= options.FreshnessAgingSeconds,
                "Party SDK freshness thresholds must be ordered and non-negative.")
            .ValidateOnStart();
        _ = services.AddSingleton<PartySdkReadModelEraser>();
        _ = services.AddSingleton<PartySdkLastKnownReadModelCache>();
        _ = services.AddScoped<PartySdkQueryService>();
        _ = services.AddSingleton<PartyPayloadProtectionService>();
        _ = services.AddSingleton<EventStorePartyPayloadProtectionAdapter>();
        _ = services.AddSingleton<IEventPayloadProtectionService>(sp => sp.GetRequiredService<EventStorePartyPayloadProtectionAdapter>());
        _ = services.AddSingleton<IPersonalDataCommandGuard, PartyPersonalDataCommandGuard>();
        PartyMemorySearchOptions memorySearch = configuration
            .GetSection(PartyMemorySearchOptions.SectionName)
            .Get<PartyMemorySearchOptions>() ?? new PartyMemorySearchOptions();
        _ = services.AddSingleton<IReadOnlyList<ErasureStoreCleanupDelegate>>(sp => {
            List<ErasureStoreCleanupDelegate> cleanups =
            [
                async (tenantId, partyId, cancellationToken) =>
                {
                    try
                    {
                        await sp.GetRequiredService<PartySdkReadModelEraser>()
                            .EraseAsync(tenantId, partyId, cancellationToken)
                            .ConfigureAwait(false);
                        return new ErasureVerificationStoreResult
                        {
                            StoreName = "sdk-read-models",
                            Status = ErasureStoreCleanupStatus.Cleaned,
                            Timestamp = DateTimeOffset.UtcNow,
                        };
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        // SDK batch failures are operational, not proof that encrypted state is
                        // unreadable. Return bounded failure so D15 cannot mis-certify cleanup.
                        return new ErasureVerificationStoreResult
                        {
                            StoreName = "sdk-read-models",
                            Status = ErasureStoreCleanupStatus.Failed,
                            Timestamp = DateTimeOffset.UtcNow,
                            ErrorMessage = "SDK read-model cleanup did not complete.",
                        };
                    }
                },
                (tenantId, partyId, cancellationToken) =>
                {
                    // Evict the in-process last-known-good cache so a degraded read cannot serve
                    // pre-erasure PII after PartySdkReadModelEraser has redacted the canonical
                    // read-model store. The cache has no relationship to the eraser itself (it
                    // lives in a different project to avoid a circular reference), so eviction is
                    // composed here as its own erasure-cleanup step.
                    PartySdkLastKnownReadModelCache cache = sp.GetRequiredService<PartySdkLastKnownReadModelCache>();
                    cache.EvictDetail(tenantId, partyId);
                    cache.EvictProcessing(tenantId, partyId);
                    cache.EvictIndex(tenantId);
                    return Task.FromResult(new ErasureVerificationStoreResult
                    {
                        StoreName = "projection-cache",
                        Status = ErasureStoreCleanupStatus.Cleaned,
                        Timestamp = DateTimeOffset.UtcNow,
                    });
                },
                // AggregateActor / EventStore.Server snapshot ownership left this host in Story 8.6.
                // Report NotApplicable rather than a false Cleaned so verification certificates stay honest.
                (tenantId, partyId, cancellationToken) => Task.FromResult(new ErasureVerificationStoreResult
                {
                    StoreName = "aggregate-readable-state",
                    Status = ErasureStoreCleanupStatus.NotApplicable,
                    Timestamp = DateTimeOffset.UtcNow,
                }),
                (tenantId, partyId, cancellationToken) => Task.FromResult(new ErasureVerificationStoreResult
                {
                    StoreName = "snapshots",
                    Status = ErasureStoreCleanupStatus.NotApplicable,
                    Timestamp = DateTimeOffset.UtcNow,
                }),
            ];

            cleanups.Add(async (tenantId, partyId, cancellationToken) =>
            {
                // Cleanup remains active even when new Memories indexing is disabled. Each
                // durable mapping carries the CaseId used at ingestion time; the current CaseId
                // is retained only as a compatibility fallback for legacy mappings. Read the
                // live options so a runtime CaseId reload matches PartyMemoryIndexEntrySearchIndexer.
                PartyMemoryCleanupService cleanupService = sp.GetRequiredService<PartyMemoryCleanupService>();
                PartyMemorySearchOptions current = sp
                    .GetRequiredService<IOptionsMonitor<PartyMemorySearchOptions>>()
                    .CurrentValue;
                PartyMemoryCleanupResult result = await cleanupService
                    .DeleteByPartyAsync(tenantId, current.CaseId ?? string.Empty, partyId, cancellationToken)
                    .ConfigureAwait(false);

                return new ErasureVerificationStoreResult
                {
                    StoreName = "memories-search",
                    Status = result.Cleaned ? ErasureStoreCleanupStatus.Cleaned : ErasureStoreCleanupStatus.Failed,
                    Timestamp = DateTimeOffset.UtcNow,
                    ErrorMessage = result.BlockedReason,
                };
            });

            return cleanups;
        });
        _ = services.AddSingleton<IErasureVerificationService, ErasureVerificationService>();
        _ = services.AddSingleton<PartyErasureOrchestrator>();

        services.AddActors(options => {
            options.Actors.RegisterActor<PartyKeyRetryActor>();
        });

        // Actor proxy factory for key-destruction retry actors.
        _ = services.AddSingleton<IActorProxyFactory>(_ => new ActorProxyFactory());

        // Search provider (local fallback until Hexalith.Memories rich search is configured)
        _ = services.AddSingleton<IPartySearchProvider, LocalFuzzyPartySearchProvider>();
        _ = services.AddSingleton<LocalPartySearchService>();
        _ = services.AddOptions<PartyMemorySearchOptions>()
            .BindConfiguration(PartyMemorySearchOptions.SectionName)
            .ValidateOnStart();
        _ = services.AddSingleton<IValidateOptions<PartyMemorySearchOptions>, PartyMemorySearchOptionsValidator>();
        _ = services
            .AddOptions<PartyMemoryUnitMappingStoreOptions>()
            .Bind(configuration.GetSection(PartyMemoryUnitMappingStoreOptions.SectionName));
        _ = services.AddSingleton<IPartyMemoryUnitMappingStore, PartyMemoryUnitMappingStore>();
        string? memoriesApiToken = memorySearch.ApiToken;
        _ = services.AddHttpClient<PartyMemoryCleanupService>((sp, httpClient) =>
        {
            if (memorySearch.Endpoint is not null)
            {
                httpClient.BaseAddress = memorySearch.Endpoint;
            }

            PartyMemoryCleanupService.ConfigureAuthorization(
                httpClient,
                memoriesApiToken,
                sp.GetService<ILogger<PartyMemoryCleanupService>>());
        });

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
            _ = services.AddSingleton<PartyMemoryIndexingService>();
            _ = services.AddSingleton<IPartySearchService>(sp => new MemoriesPartySearchService(
                sp.GetRequiredService<MemoriesClient>(),
                sp.GetRequiredService<LocalPartySearchService>(),
                sp.GetRequiredService<IOptionsMonitor<PartyMemorySearchOptions>>(),
                sp.GetRequiredService<ILogger<MemoriesPartySearchService>>()));
        }
        else
        {
            // Local fallback is the only registered IPartySearchService when Memories is disabled.
            _ = services.AddSingleton<IPartySearchService>(sp => sp.GetRequiredService<LocalPartySearchService>());
        }

        _ = services.AddSingleton<IPartyIndexSearchIndexer>(sp => new PartyMemoryIndexEntrySearchIndexer(
            sp.GetService<PartyMemoryIndexingService>(),
            sp.GetRequiredService<PartyMemoryCleanupService>(),
            sp.GetRequiredService<IOptionsMonitor<PartyMemorySearchOptions>>(),
            sp.GetRequiredService<ILogger<PartyMemoryIndexEntrySearchIndexer>>()));

        // FluentValidation (assembly scanning — no explicit validator registration)
        _ = services.AddValidatorsFromAssemblyContaining<CreatePartyValidator>();

        _ = services.ConfigureHttpJsonOptions(options => PartiesJsonOptions.ApplyTo(options.SerializerOptions));

        return services;
    }

    private static IEnumerable<string> PartyDomainCaseVariants()
    {
        char[] source = PartyDomain.ToCharArray();
        int variantCount = 1 << source.Length;
        for (int mask = 0; mask < variantCount; mask++)
        {
            char[] value = new char[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                value[index] = (mask & (1 << index)) == 0
                    ? char.ToLowerInvariant(source[index])
                    : char.ToUpperInvariant(source[index]);
            }

            yield return new string(value);
        }
    }
}
