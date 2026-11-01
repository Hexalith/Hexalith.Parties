using System.Security.Cryptography;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Checks actual SDK signed workload admission with the private profile's narrowed receiver audience.</summary>
public sealed class PrivateCatalogWorkloadAuthenticationTests
{
    private const string Scheme = "PrivateCatalogWorkload";
    private const string Receiver = "sample-private-session";
    private const string Gateway = "eventstore-private-session";

    /// <summary>Shared trust remains exact; an incomplete dedicated contract continues to refuse.</summary>
    [Theory]
    [InlineData("valid", true)]
    [InlineData("wrong-audience", false)]
    [InlineData("wrong-issuer", false)]
    [InlineData("wrong-key", false)]
    [InlineData("wrong-caller", false)]
    [InlineData("partial-dedicated", false)]
    public async Task WorkloadVerifier_RequiresActualSignedIdentityAndCompleteContractAsync(string scenario, bool accepted)
    {
        string key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        const string Issuer = "private-catalog-fixture";
        await using WebApplication receiver = Build(key, Issuer, Gateway, scenario == "partial-dedicated");
        await using WebApplication caller = Build(scenario == "wrong-key" ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) : key,
            scenario == "wrong-issuer" ? "foreign-catalog-fixture" : Issuer,
            scenario == "wrong-caller" ? "foreign-gateway" : Gateway, false);
        string? assertion = await caller.Services.GetRequiredService<IWorkloadAssertionIssuer>().IssueAsync(
            new WorkloadAssertionRequest(scenario == "wrong-audience" ? "foreign-receiver" : Receiver,
                EventStoreWorkloadOperations.DomainServiceProcess), TestContext.Current.CancellationToken).ConfigureAwait(true);
        assertion.ShouldNotBeNull();
        JwtBearerOptions options = receiver.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);
        ConfigureWorkloadJwtBearerOptions.IsConfigured(options).ShouldBe(scenario != "partial-dedicated");
        if (scenario != "partial-dedicated")
        {
            options.TokenValidationParameters.ValidAudiences.ShouldBe([Receiver]);
        }

        WorkloadAssertionEvaluation result = await receiver.Services.GetRequiredService<WorkloadAssertionValidator>()
            .ValidateAsync(Scheme, assertion, EventStoreWorkloadOperations.DomainServiceProcess, null,
                TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Succeeded.ShouldBe(accepted);
        if (accepted)
        {
            result.Caller.ShouldBe(Gateway);
            result.Operations.ShouldContain(EventStoreWorkloadOperations.DomainServiceProcess);
        }
        else
        {
            result.ReasonCode.ShouldNotBeNullOrWhiteSpace();
            if (scenario == "partial-dedicated") { result.ReasonCode.ShouldBe(WorkloadAuthenticationReasons.VerifierUnconfigured); }
        }
    }

    private static WebApplication Build(string key, string issuer, string workload, bool partialDedicated)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        Dictionary<string, string?> settings = new()
        {
            ["Authentication:JwtBearer:Authority"] = string.Empty,
            ["Authentication:JwtBearer:Issuer"] = issuer,
            ["Authentication:JwtBearer:Audience"] = "public-fixture-api",
            ["Authentication:JwtBearer:SigningKey"] = key,
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["Authentication:Workload:Audience"] = Receiver,
            ["Authentication:Workload:AllowedCallers:0"] = Gateway,
            ["Authentication:WorkloadIssuer:Workload"] = workload,
        };
        if (partialDedicated) { settings["Authentication:WorkloadJwtBearer:Audience"] = Receiver; }
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(Scheme, "Authentication:Workload");
        builder.Services.AddEventStoreWorkloadAssertionIssuer(builder.Configuration);
        return builder.Build();
    }
}
