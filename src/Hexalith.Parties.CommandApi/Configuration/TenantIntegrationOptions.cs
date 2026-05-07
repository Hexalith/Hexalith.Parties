using Microsoft.Extensions.Options;

namespace Hexalith.Parties.CommandApi.Configuration;

/// <summary>
/// Parties-side integration settings for the Hexalith.Tenants authority.
/// </summary>
public sealed class TenantIntegrationOptions
{
    public const string SectionName = "Tenants";

    public bool Enabled { get; set; }

    public string? ServiceName { get; set; }

    public string? CommandApiAppId { get; set; }

    public string? PubSubName { get; set; }

    public string? TopicName { get; set; }
}

/// <summary>
/// Validates Tenants integration only when tenant authorization is enabled.
/// </summary>
public sealed class TenantIntegrationOptionsValidator : IValidateOptions<TenantIntegrationOptions>
{
    public ValidateOptionsResult Validate(string? name, TenantIntegrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        AddIfMissing(failures, options.ServiceName, "Tenants:ServiceName");
        AddIfMissing(failures, options.CommandApiAppId, "Tenants:CommandApiAppId");
        AddIfMissing(failures, options.PubSubName, "Tenants:PubSubName");
        AddIfMissing(failures, options.TopicName, "Tenants:TopicName");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddIfMissing(List<string> failures, string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{key} is required when Tenants:Enabled is true.");
        }
    }
}
