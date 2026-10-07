using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;

using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Client.Tests;

public sealed class DependencyInjectionTests
{
    /// <summary>The production typed identity client resolves with either the default or an injected completion clock.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddPartiesClient_ResolvesIPartiesIdentityClient(bool registerClock)
    {
        var services = new ServiceCollection();
        if (registerClock)
        {
            services.AddSingleton(NSubstitute.Substitute.For<TimeProvider>());
        }

        services.AddPartiesClient(BuildConfiguration());
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IPartiesIdentityClient>().ShouldBeOfType<HttpPartiesIdentityClient>();
    }

    /// <summary>Controlled replies through the production registration use the injected completion clock and exclusive expiry.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddPartiesClient_IdentityAcceptanceUsesRegisteredClock(bool atExpiry)
    {
        DateTimeOffset start = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiry = start.AddDays(365);
        const string actor = "01HX0000000000000000000001";
        var binding = new HumanActorBindingEvidence("tenant-a", "party-1", actor, 1, 1, start, expiry, "synthetic-writer", "synthetic-operator",
            new("synthetic-365-v1", "party-actor-history-v1", expiry, 1, "synthetic-custody", true, true, true));
        var result = new PartyIdentityResult(PartyIdentityOutcome.Resolved,
            new(1, "tenant-a", "party-1", PartyIdentityClassification.Human, true, false, false, 2, start.AddDays(1), "synthetic-observation", binding));
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(atExpiry ? expiry : expiry.AddTicks(-1));
        var services = new ServiceCollection();
        services.AddSingleton(clock);
        services.AddPartiesClient(BuildConfiguration());
        int requests = 0;
        services.AddHttpClient<IPartiesIdentityClient, HttpPartiesIdentityClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new IdentityReplyHttpMessageHandler(request =>
            {
                request.RequestUri!.AbsolutePath.ShouldBe("/api/v1/queries");
                requests++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new SubmitQueryResponse("synthetic-correlation",
                        JsonSerializer.SerializeToElement(result, PartiesJsonOptions.Default)), options: PartiesJsonOptions.Default),
                };
            }));
        using ServiceProvider provider = services.BuildServiceProvider();
        IPartiesIdentityClient client = provider.GetRequiredService<IPartiesIdentityClient>();
        if (atExpiry)
        {
            PartiesClientException failure = await Should.ThrowAsync<PartiesClientException>(() => client.ResolvePartyIdentityAsync("tenant-a",
                new("tenant-a", "party-1", actor), TestContext.Current.CancellationToken));
            failure.Status.ShouldBe(503);
        }
        else
        {
            PartyIdentityResult resolved = await client.ResolvePartyIdentityAsync("tenant-a", new("tenant-a", "party-1", actor), TestContext.Current.CancellationToken);
            resolved.Outcome.ShouldBe(PartyIdentityOutcome.Resolved);
            resolved.Evidence!.HumanBinding.ShouldBe(binding);
        }

        requests.ShouldBe(1);
        clock.Received(1).GetUtcNow();
    }

    [Fact]
    public void AddPartiesClient_ResolvesIPartiesCommandClient()
    {
        ServiceProvider provider = BuildProvider();

        IPartiesCommandClient client = provider.GetRequiredService<IPartiesCommandClient>();

        client.ShouldNotBeNull();
        client.ShouldBeOfType<HttpPartiesCommandClient>();
    }

    [Fact]
    public void AddPartiesClient_ResolvesIPartiesQueryClient()
    {
        ServiceProvider provider = BuildProvider();

        IPartiesQueryClient client = provider.GetRequiredService<IPartiesQueryClient>();

        client.ShouldNotBeNull();
        client.ShouldBeOfType<HttpPartiesQueryClient>();
    }

    [Fact]
    public void AddPartiesClient_ReturnsServiceCollectionForFluentChaining()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = BuildConfiguration();

        IServiceCollection result = services.AddPartiesClient(configuration);

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddPartiesClient_RegistersResolvedOptions()
    {
        ServiceProvider provider = BuildProvider();

        PartiesClientOptions options = provider.GetRequiredService<IOptions<PartiesClientOptions>>().Value;

        options.BaseUrl.ShouldBe("https://localhost:5001");
        options.Tenant.ShouldBe("tenant-a");
    }

    [Fact]
    public void AddPartiesClient_ThrowsWhenBaseUrlIsMissing()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        Should.Throw<InvalidOperationException>(() => services.AddPartiesClient(configuration))
            .Message.ShouldContain("Parties:BaseUrl configuration is required.");
    }

    [Fact]
    public void AddPartiesClient_ThrowsWhenBaseUrlIsRelative()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Parties:BaseUrl"] = "/relative",
                ["Parties:Tenant"] = "tenant-a",
            })
            .Build();

        Should.Throw<InvalidOperationException>(() => services.AddPartiesClient(configuration))
            .Message.ShouldContain("Parties:BaseUrl must be an absolute URI.");
    }

    [Fact]
    public void AddPartiesClient_ThrowsWhenBaseUrlUsesUnsupportedScheme()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Parties:BaseUrl"] = "ftp://localhost",
                ["Parties:Tenant"] = "tenant-a",
            })
            .Build();

        Should.Throw<InvalidOperationException>(() => services.AddPartiesClient(configuration))
            .Message.ShouldContain("Parties:BaseUrl must use http or https.");
    }

    [Fact]
    public void AddPartiesClient_ThrowsWhenTenantIsMissing()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Parties:BaseUrl"] = "https://localhost:5001",
            })
            .Build();

        Should.Throw<InvalidOperationException>(() => services.AddPartiesClient(configuration))
            .Message.ShouldContain("Parties:Tenant configuration is required.");
    }

    [Fact]
    public void AddPartiesClient_ThrowsWhenTenantIsBlank()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Parties:BaseUrl"] = "https://localhost:5001",
                ["Parties:Tenant"] = " ",
            })
            .Build();

        Should.Throw<InvalidOperationException>(() => services.AddPartiesClient(configuration))
            .Message.ShouldContain("Parties:Tenant configuration is required.");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = BuildConfiguration();

        services.AddPartiesClient(configuration);

        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Parties:BaseUrl"] = "https://localhost:5001",
                ["Parties:Tenant"] = "tenant-a",
            })
            .Build();
    }
}
