using System.Security.Cryptography;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Parties.Compliance;
using Hexalith.Parties.Contracts.Security;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Runs the real Parties host with ephemeral Development credentials and the unchanged SDK security boundary.</summary>
internal sealed class PartiesProcessTestFactory : WebApplicationFactory<Program>
{
    /// <summary>Gets the isolated sidecar channel token, with no production authority.</summary>
    internal const string ChannelToken = "parties-owner-local-channel-token";
    private readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly IDomainProcessor? _registeredProcessor;
    private readonly bool _gdprFeaturesActive;
    private readonly bool _useProductionProcessor;
    private readonly IEventPayloadProtectionService? _payloadProtectionService;

    /// <summary>Creates a capturing processor fixture.</summary>
    public PartiesProcessTestFactory(bool gdprFeaturesActive = false)
    {
        Processor = new CapturingDomainProcessor();
        _registeredProcessor = Processor;
        _gdprFeaturesActive = gdprFeaturesActive;
    }

    /// <summary>Creates a production replay/validation fixture with optional isolated protection.</summary>
    public PartiesProcessTestFactory(bool useProductionProcessor, bool gdprFeaturesActive = false,
        IEventPayloadProtectionService? payloadProtectionService = null)
    {
        _useProductionProcessor = useProductionProcessor;
        _payloadProtectionService = payloadProtectionService;
        _gdprFeaturesActive = gdprFeaturesActive;
    }

    /// <summary>Creates a fixture using an explicit test processor.</summary>
    public PartiesProcessTestFactory(IDomainProcessor registeredProcessor, bool gdprFeaturesActive = false)
    {
        ArgumentNullException.ThrowIfNull(registeredProcessor);
        Processor = new CapturingDomainProcessor();
        _registeredProcessor = registeredProcessor;
        _gdprFeaturesActive = gdprFeaturesActive;
    }

    /// <summary>Gets the capturing processor when this fixture owns one.</summary>
    public CapturingDomainProcessor? Processor { get; }

    /// <summary>Creates a client carrying a real SDK-issued assertion for exactly one audience and operation.</summary>
    public HttpClient CreateAuthenticatedClient(string operation = EventStoreWorkloadOperations.DomainServiceProcess,
        string audience = "parties")
    {
        HttpClient client = CreateClient();
        string assertion = Services.GetRequiredService<IWorkloadAssertionIssuer>()
            .IssueAsync(new WorkloadAssertionRequest(audience, operation), TestContext.Current.CancellationToken)
            .AsTask().GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("The isolated fixture could not issue its workload assertion.");
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        return client;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Development");
        builder.UseSetting(MvpComplianceWarning.ActivationConfigurationKey, _gdprFeaturesActive.ToString());
        var settings = new Dictionary<string, string>
        {
            [DaprAppChannelToken.ConfigurationKey] = ChannelToken,
            ["EventStore:DomainService:AppId"] = "parties",
            ["Authentication:JwtBearer:Issuer"] = "parties-owner-local-issuer",
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:SigningKey"] = _signingKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.HmacSha256,
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["Authentication:WorkloadIssuer:Workload"] = "eventstore",
        };
        foreach (KeyValuePair<string, string> setting in settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }

        builder.ConfigureTestServices(services =>
        {
            // This fixture qualifies HTTP authentication, never backend readiness.
            // Prevent Dapr/tenant health probes from invoking unqualified live seams.
            HealthCheckService health = Substitute.For<HealthCheckService>();
            health.CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero)));
            services.AddSingleton(health);

            if (_payloadProtectionService is not null)
            {
                services.AddSingleton(_payloadProtectionService);
                services.AddSingleton(Substitute.For<IPartyErasureRecordStore>());
            }

            if (!_useProductionProcessor)
            {
                services.AddKeyedSingleton<IDomainProcessor>("party", (_, _) => _registeredProcessor!);
                services.AddKeyedSingleton<IAsyncDomainProcessor>("party", (_, _) => (IAsyncDomainProcessor)_registeredProcessor!);
            }
        });
    }
}
