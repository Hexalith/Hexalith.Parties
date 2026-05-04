using System.Collections.Frozen;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.CommandApi.Search;

internal sealed class PartyMemorySearchOptions
{
    public const string SectionName = "Parties:MemoriesSearch";

    private static readonly string[] s_defaultAxes = ["hybrid", "syntactic", "semantic", "graph"];

    public bool Enabled { get; init; }

    public Uri? Endpoint { get; init; }

    public string? ApiToken { get; init; }

    public bool RequireApiToken { get; init; } = true;

    public string? TenantId { get; init; }

    public string? CaseId { get; init; }

    /// <summary>
    /// Backing array — kept as <see cref="string"/>[] so configuration binding (which writes
    /// raw arrays into <c>init</c> setters) works without a custom binder. Do not mutate the
    /// returned array; use <see cref="IsAxisEnabled(string)"/> for runtime checks.
    /// </summary>
    public string[] EnabledAxes { get; init; } = s_defaultAxes;

    public bool IsAxisEnabled(string axis)
    {
        if (string.IsNullOrWhiteSpace(axis))
        {
            return false;
        }

        string[]? axes = EnabledAxes;
        if (axes is null || axes.Length == 0)
        {
            return false;
        }

        foreach (string a in axes)
        {
            if (string.Equals(a, axis, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class PartyMemorySearchOptionsValidator : IValidateOptions<PartyMemorySearchOptions>
{
    private static readonly FrozenSet<string> s_allowedAxes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hybrid",
        "syntactic",
        "semantic",
        "graph",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

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
        else if (!options.Endpoint.AbsoluteUri.EndsWith('/'))
        {
            // HttpClient relative-path resolution drops the base path component when the
            // base address has no trailing slash (e.g. "https://example.com/v1" + "api/foo"
            // resolves to "https://example.com/api/foo", silently 404). Reject early so
            // operators see the misconfiguration instead of cleanup reporting "Cleaned: true"
            // on a bogus 404.
            failures.Add($"{nameof(PartyMemorySearchOptions.Endpoint)} must end with a trailing slash so HttpClient relative paths resolve under the configured base path.");
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

        // Null-guard EnabledAxes so a malformed configuration (e.g. `EnabledAxes` bound to
        // `null` via deserialization) returns a validation failure instead of throwing
        // NullReferenceException out of the validator pipeline.
        string[]? axes = options.EnabledAxes;
        if (axes is null || axes.Length == 0)
        {
            failures.Add($"{nameof(PartyMemorySearchOptions.EnabledAxes)} must include at least one axis.");
        }
        else
        {
            foreach (string axis in axes)
            {
                if (string.IsNullOrWhiteSpace(axis) || !s_allowedAxes.Contains(axis))
                {
                    failures.Add($"{nameof(PartyMemorySearchOptions.EnabledAxes)} contains unsupported axis '{axis}'.");
                }
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
