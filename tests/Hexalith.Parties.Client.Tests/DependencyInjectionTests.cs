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
    public void AddPartiesClient_ResolvesIPartiesCommandClientAsync()
    {
        ServiceProvider provider = BuildProvider();

        IPartiesCommandClient client = provider.GetRequiredService<IPartiesCommandClient>();

        client.ShouldNotBeNull();
        client.ShouldBeOfType<HttpPartiesCommandClient>();
    }

    [Fact]
    public void AddPartiesClient_ResolvesIPartiesQueryClientAsync()
    {
        ServiceProvider provider = BuildProvider();

        IPartiesQueryClient client = provider.GetRequiredService<IPartiesQueryClient>();

        client.ShouldNotBeNull();
        client.ShouldBeOfType<HttpPartiesQueryClient>();
    }

    [Fact]
    public void AddPartiesClient_ReturnsServiceCollectionForFluentChainingAsync()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = BuildConfiguration();

        IServiceCollection result = services.AddPartiesClient(configuration);

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddPartiesClient_RegistersResolvedOptionsAsync()
    {
        ServiceProvider provider = BuildProvider();

        PartiesClientOptions options = provider.GetRequiredService<IOptions<PartiesClientOptions>>().Value;

        options.BaseUrl.ShouldBe("https://localhost:5001");
    }

    [Fact]
    public void AddPartiesClient_ThrowsWhenBaseUrlIsMissingAsync()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        Should.Throw<InvalidOperationException>(() => services.AddPartiesClient(configuration))
            .Message.ShouldContain("Parties:BaseUrl configuration is required.");
    }

    [Fact]
    public void AddPartiesClient_ThrowsWhenBaseUrlIsRelativeAsync()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Parties:BaseUrl"] = "/relative",
            })
            .Build();

        Should.Throw<InvalidOperationException>(() => services.AddPartiesClient(configuration))
            .Message.ShouldContain("Parties:BaseUrl must be an absolute URI.");
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
            })
            .Build();
    }
}
