using Hexalith.Parties.CommandApi.Search;
using Hexalith.Parties.CommandApi.Extensions;
using Hexalith.Memories.Client.Rest;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.Search;

public class PartyMemorySearchOptionsValidatorTests
{
    private readonly PartyMemorySearchOptionsValidator _validator = new();

    [Fact]
    public void DisabledOptionsDoNotRequireEndpointAuthTenantOrCase()
    {
        ValidateOptionsResult result = _validator.Validate(
            PartyMemorySearchOptions.SectionName,
            new PartyMemorySearchOptions { Enabled = false });

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void EnabledOptionsRequireEndpointTenantCaseAndAuthWhenTokenIsRequired()
    {
        ValidateOptionsResult result = _validator.Validate(
            PartyMemorySearchOptions.SectionName,
            new PartyMemorySearchOptions
            {
                Enabled = true,
                RequireApiToken = true,
                EnabledAxes = ["hybrid"],
            });

        result.Failed.ShouldBeTrue();
        string.Join(" ", result.Failures).ShouldContain(nameof(PartyMemorySearchOptions.Endpoint));
        string.Join(" ", result.Failures).ShouldContain(nameof(PartyMemorySearchOptions.TenantId));
        string.Join(" ", result.Failures).ShouldContain(nameof(PartyMemorySearchOptions.CaseId));
        string.Join(" ", result.Failures).ShouldContain(nameof(PartyMemorySearchOptions.ApiToken));
    }

    [Fact]
    public void EnabledOptionsRejectUnsupportedAxes()
    {
        ValidateOptionsResult result = _validator.Validate(
            PartyMemorySearchOptions.SectionName,
            new PartyMemorySearchOptions
            {
                Enabled = true,
                Endpoint = new Uri("https://memories.example"),
                TenantId = "tenant-a",
                CaseId = "case-a",
                RequireApiToken = false,
                EnabledAxes = ["hybrid", "unsupported"],
            });

        result.Failed.ShouldBeTrue();
        string.Join(" ", result.Failures).ShouldContain("unsupported");
    }

    [Fact]
    public void AddPartiesRegistersMemoriesClientWhenMemorySearchIsEnabled()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Parties:MemoriesSearch:Enabled"] = "true",
                ["Parties:MemoriesSearch:Endpoint"] = "https://memories.example/",
                ["Parties:MemoriesSearch:RequireApiToken"] = "false",
                ["Parties:MemoriesSearch:TenantId"] = "tenant-a",
                ["Parties:MemoriesSearch:CaseId"] = "case-a",
            })
            .Build();

        ServiceProvider provider = new ServiceCollection()
            .AddLogging()
            .AddParties(configuration)
            .BuildServiceProvider();

        MemoriesClient client = provider.GetRequiredService<MemoriesClient>();

        client.BaseAddress.ShouldBe(new Uri("https://memories.example/"));
    }
}
