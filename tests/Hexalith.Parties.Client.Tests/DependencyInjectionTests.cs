using Hexalith.Parties.Client.Abstractions;
using Hexalith.Parties.Client.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Client.Tests;

public sealed class DependencyInjectionTests
{
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
