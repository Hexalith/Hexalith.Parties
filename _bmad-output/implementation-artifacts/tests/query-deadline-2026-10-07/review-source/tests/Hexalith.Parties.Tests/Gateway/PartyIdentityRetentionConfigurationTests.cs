using Hexalith.EventStore.Contracts.Security;
using Hexalith.Parties.Authorization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Verifies the approved host policy through the same options binding used by Parties.</summary>
public sealed class PartyIdentityRetentionConfigurationTests
{
    /// <summary>Operational query timeouts bind independently of the accepted history lifetime.</summary>
    [Theory]
    [InlineData("00:00:05", 5)]
    [InlineData("00:00:00", 0)]
    [InlineData("00:00:31", 31)]
    public void HostConfiguration_QueryTimeoutBindsWithoutChangingRetention(string configured, int seconds)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddConfiguration(LoadConfiguration())
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Parties:Identity:QueryTimeout"] = configured }).Build();
        using ServiceProvider services = new ServiceCollection()
            .AddOptions<PartyIdentityOptions>().Bind(configuration.GetSection("Parties:Identity"))
            .Services.BuildServiceProvider();
        PartyIdentityOptions options = services.GetRequiredService<IOptionsMonitor<PartyIdentityOptions>>().CurrentValue;
        options.QueryTimeout.ShouldBe(TimeSpan.FromSeconds(seconds));
        options.Policy!.Retention.ShouldBe(TimeSpan.FromDays(365));
    }

    /// <summary>The unconfigured operational budget is the existing SDK maximum, without supplying a retention policy.</summary>
    [Fact]
    public void QueryTimeout_DefaultIsThirtySecondsWithoutImplicitRetentionPolicy()
    {
        var options = new PartyIdentityOptions();
        options.QueryTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.Policy.ShouldBeNull();
    }

    /// <summary>The actual host JSON supplies fixed days, without extending the clock after erasure or restore.</summary>
    [Fact]
    public void HostConfiguration_BindsApproved365FixedDays()
    {
        IConfigurationRoot configuration = LoadConfiguration();
        using ServiceProvider services = new ServiceCollection()
            .AddOptions<PartyIdentityOptions>().Bind(configuration.GetSection("Parties:Identity"))
            .Services.BuildServiceProvider();
        IdentityHistoryPolicy policy = services.GetRequiredService<IOptionsMonitor<PartyIdentityOptions>>().CurrentValue.Policy!;
        policy.ShouldNotBeNull();
        policy.PolicyId.ShouldBe("party-actor-retention-v1");
        policy.ExpiryTrigger.ShouldBe("binding-effective-at");
        policy.Retention.TotalSeconds.ShouldBe(31_536_000);
        var effectiveAt = new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);
        policy.DeriveExpiry(effectiveAt).ShouldBe(new DateTimeOffset(2028, 2, 29, 0, 0, 0, TimeSpan.Zero));
        services.GetService<IIdentityHistoryCustody>().ShouldBeNull();
    }

    /// <summary>A later configuration source can withdraw policy without installing a default.</summary>
    [Fact]
    public void HostConfiguration_PolicyWithdrawalDeniesBindings()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddConfiguration(LoadConfiguration())
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Parties:Identity:PolicyId"] = "" }).Build();
        using ServiceProvider services = new ServiceCollection()
            .AddOptions<PartyIdentityOptions>().Bind(configuration.GetSection("Parties:Identity"))
            .Services.BuildServiceProvider();
        services.GetRequiredService<IOptionsMonitor<PartyIdentityOptions>>().CurrentValue.Policy.ShouldBeNull();
        new PartyIdentityOptions().Policy.ShouldBeNull();
    }

    private static IConfigurationRoot LoadConfiguration() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Configuration", "parties-appsettings.json"), optional: false)
        .Build();
}
