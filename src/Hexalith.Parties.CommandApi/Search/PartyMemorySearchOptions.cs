using Microsoft.Extensions.Options;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed class PartyMemorySearchOptions
{
    public const string SectionName = "Parties:MemoriesSearch";

    public bool Enabled { get; init; }

    public Uri? Endpoint { get; init; }

    public string? ApiToken { get; init; }

    public bool RequireApiToken { get; init; } = true;

    public string? TenantId { get; init; }

    public string? CaseId { get; init; }

    public string[] EnabledAxes { get; init; } = ["hybrid", "syntactic", "semantic", "graph"];
}

internal sealed class PartyMemorySearchOptionsValidator : IValidateOptions<PartyMemorySearchOptions>
{
    private static readonly HashSet<string> s_allowedAxes = new(StringComparer.OrdinalIgnoreCase)
    {
        "hybrid",
        "syntactic",
        "semantic",
        "graph",
    };

    public ValidateOptionsResult Validate(string? name, PartyMemorySearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];

        if (options.Endpoint is null || !options.Endpoint.IsAbsoluteUri)
        {
            failures.Add($"{nameof(PartyMemorySearchOptions.Endpoint)} must be an absolute URI when Memories search is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.TenantId))
        {
            failures.Add($"{nameof(PartyMemorySearchOptions.TenantId)} is required when Memories search is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.CaseId))
        {
            failures.Add($"{nameof(PartyMemorySearchOptions.CaseId)} is required when Memories search is enabled.");
        }

        if (options.RequireApiToken && string.IsNullOrWhiteSpace(options.ApiToken))
        {
            failures.Add($"{nameof(PartyMemorySearchOptions.ApiToken)} is required when {nameof(PartyMemorySearchOptions.RequireApiToken)} is true.");
        }

        if (options.EnabledAxes.Length == 0)
        {
            failures.Add($"{nameof(PartyMemorySearchOptions.EnabledAxes)} must include at least one axis.");
        }

        foreach (string axis in options.EnabledAxes)
        {
            if (!s_allowedAxes.Contains(axis))
            {
                failures.Add($"{nameof(PartyMemorySearchOptions.EnabledAxes)} contains unsupported axis '{axis}'.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
