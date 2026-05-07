using Hexalith.Parties.Configuration;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.Configuration;

public sealed class TenantIntegrationOptionsValidatorTests
{
    private readonly TenantIntegrationOptionsValidator _validator = new();

    [Fact]
    public void Validate_WhenTenantsDisabled_AllowsMissingIntegrationSettings()
    {
        ValidateOptionsResult result = _validator.Validate(
            TenantIntegrationOptions.SectionName,
            new TenantIntegrationOptions { Enabled = false });

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenTenantsEnabledAndRequiredSettingsMissing_FailsWithActionableKeys()
    {
        ValidateOptionsResult result = _validator.Validate(
            TenantIntegrationOptions.SectionName,
            new TenantIntegrationOptions { Enabled = true });

        result.Failed.ShouldBeTrue();
        string failures = string.Join(Environment.NewLine, result.Failures);
        failures.ShouldContain("Tenants:ServiceName");
        failures.ShouldContain("Tenants:CommandApiAppId");
        failures.ShouldContain("Tenants:PubSubName");
        failures.ShouldContain("Tenants:TopicName");
    }

    [Fact]
    public void Validate_WhenTenantsEnabledAndRequiredSettingsPresent_Succeeds()
    {
        ValidateOptionsResult result = _validator.Validate(
            TenantIntegrationOptions.SectionName,
            new TenantIntegrationOptions
            {
                Enabled = true,
                ServiceName = "tenants",
                CommandApiAppId = "parties",
                PubSubName = "pubsub",
                TopicName = "system.tenants.events",
            });

        result.Succeeded.ShouldBeTrue();
    }
}
