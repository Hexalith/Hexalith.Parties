using System.Collections.Frozen;

using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Search;

internal sealed class PartyMemorySearchOptions
{
    public const string SectionName = "Parties:MemoriesSearch";

    public bool Enabled { get; init; }

    public Uri? Endpoint { get; init; }

    public string? ApiToken { get; init; }

    public bool RequireApiToken { get; init; } = true;

    public string? TenantId { get; init; }

    public string? CaseId { get; init; }

    /// <summary>
    /// Backing array — kept as <see cref="string"/>[] so configuration binding (which writes
    /// raw arrays into <c>init</c> setters) works without a custom binder. Returns a fresh
    /// array per instance so a consumer accidentally mutating an element does not poison the
    /// default for every other not-yet-constructed options instance. Prefer
    /// <see cref="IsAxisEnabled(string)"/> for runtime checks.
    /// </summary>
    public string[] EnabledAxes { get; init; } = ["hybrid", "syntactic", "semantic", "graph"];

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

        // Trim each axis on read so configuration providers that yield " hybrid"
        // (multi-line YAML list) do not silently mismatch a syntactically valid config.
        ReadOnlySpan<char> needle = axis.AsSpan().Trim();
        foreach (string a in axes)
        {
            if (a is null)
            {
                continue;
            }

            ReadOnlySpan<char> candidate = a.AsSpan().Trim();
            if (candidate.Equals(needle, StringComparison.OrdinalIgnoreCase))
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
        else if (!IsEndpointBaseAddressShape(options.Endpoint))
        {
            // The endpoint must function as an HttpClient BaseAddress: HttpClient relative-path
            // resolution drops the base path component when the base address has no trailing
            // slash on its path (e.g. "https://example.com/v1" + "api/foo" resolves to
            // "https://example.com/api/foo", silently 404). Reject endpoints that carry a
            // query or fragment, and reject those whose AbsolutePath does not end with '/'.
            // Operators authoring AppHost / dev-config endpoints without a trailing slash see
            // the failure here rather than cleanup reporting "Cleaned: true" on a bogus 404.
            failures.Add($"{nameof(PartyMemorySearchOptions.Endpoint)} path must end with a trailing slash so HttpClient relative paths resolve under the configured base path; query strings and fragments are not supported.");
        }

        if (string.IsNullOrWhiteSpace(options.TenantId))
        {
            failures.Add($"{nameof(PartyMemorySearchOptions.TenantId)} is required when Memories search is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.CaseId))
        {
            failures.Add($"{nameof(PartyMemorySearchOptions.CaseId)} is required when Memories search is enabled.");
        }

        if (options.RequireApiToken)
        {
            if (string.IsNullOrWhiteSpace(options.ApiToken))
            {
                failures.Add($"{nameof(PartyMemorySearchOptions.ApiToken)} is required when {nameof(PartyMemorySearchOptions.RequireApiToken)} is true.");
            }
            else if (ContainsControlOrLineBreak(options.ApiToken))
            {
                // P7: HTTP header values reject control characters / line breaks. Catching
                // this here surfaces "fail at startup" instead of "every cleanup call 401s
                // because ConfigureAuthorization silently dropped the token at runtime."
                failures.Add($"{nameof(PartyMemorySearchOptions.ApiToken)} must not contain control characters or line breaks (cannot be used as an HTTP Authorization header value).");
            }
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
                string trimmed = axis?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(trimmed) || !s_allowedAxes.Contains(trimmed))
                {
                    failures.Add($"{nameof(PartyMemorySearchOptions.EnabledAxes)} contains unsupported axis '{axis}' (allowed: hybrid, syntactic, semantic, graph; whitespace trimmed before matching).");
                }
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsEndpointBaseAddressShape(Uri endpoint)
        => string.IsNullOrEmpty(endpoint.Query)
            && string.IsNullOrEmpty(endpoint.Fragment)
            && endpoint.AbsolutePath.EndsWith('/');

    private static bool ContainsControlOrLineBreak(string token)
    {
        foreach (char c in token)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
