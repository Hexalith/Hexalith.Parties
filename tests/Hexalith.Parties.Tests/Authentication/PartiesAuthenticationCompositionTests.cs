using Hexalith.Parties.Authentication;
using Hexalith.Parties.Extensions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.Parties.Tests.Authentication;

public sealed class PartiesAuthenticationCompositionTests
{
    [Fact]
    public void AddPartiesRegistersSharedClaimsTransformationForActorHost()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Issuer"] = "hexalith-test",
                ["Authentication:JwtBearer:Audience"] = "hexalith-parties",
                ["Authentication:JwtBearer:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars-MustBeSecure!",
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                ["Tenants:PubSubName"] = "pubsub",
                ["Tenants:TopicName"] = "system.tenants.events",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddParties(configuration);

        services.ShouldContain(descriptor =>
            descriptor.ServiceType == typeof(IClaimsTransformation)
            && descriptor.ImplementationType == typeof(PartiesClaimsTransformation)
            && descriptor.Lifetime == ServiceLifetime.Transient);

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IClaimsTransformation>().ShouldBeOfType<PartiesClaimsTransformation>();
    }
}
