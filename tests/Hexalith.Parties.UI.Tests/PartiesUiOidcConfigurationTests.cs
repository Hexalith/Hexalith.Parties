using System.Text.Json;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// AC3 secret-hygiene guards for the Story 1.2 OIDC config scaffold. The host advertises the
/// <c>Authentication:OpenIdConnect</c> override shape in <c>appsettings.json</c> with EMPTY values
/// only — the real authority/client/secret arrive at runtime via <c>__</c>-nested env from the
/// AppHost (run mode) or a secret store (publish). No client secret may be committed to any
/// <c>appsettings*.json</c>. Validated against the checked-in config files, not a running host.
/// </summary>
public sealed class PartiesUiOidcConfigurationTests
{
    // The throwaway dev-realm secret lives only in the AppHost run block + the local realm import —
    // never in the UI host's committed config. A literal match here would be a real leak.
    private const string DevClientSecret = "parties-ui-dev-secret";

    private static readonly string[] OidcScaffoldKeys = ["Authority", "ClientId", "ClientSecret", "Audience"];

    [Fact]
    public void AppSettings_DeclaresEmptyOidcScaffold_SoTheOverrideShapeIsDiscoverable()
    {
        string appSettingsPath = Path.Combine(UiProjectDirectory(), "appsettings.json");
        File.Exists(appSettingsPath).ShouldBeTrue($"Missing UI appsettings.json at {appSettingsPath}.");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        JsonElement oidc = document.RootElement
            .GetProperty("Authentication")
            .GetProperty("OpenIdConnect");

        foreach (string key in OidcScaffoldKeys)
        {
            oidc.TryGetProperty(key, out JsonElement value)
                .ShouldBeTrue($"OIDC scaffold must declare '{key}' so Authentication__OpenIdConnect__{key} is overridable.");
            value.ValueKind.ShouldBe(JsonValueKind.String, $"OIDC key '{key}' must be a string.");
            value.GetString().ShouldBe(string.Empty, $"OIDC key '{key}' must ship empty (no committed value).");
        }
    }

    [Fact]
    public void NoCommittedAppSettingsFile_ContainsTheDevClientSecret()
    {
        // Sweep every appsettings*.json the UI host ships (base + Development) for the dev secret.
        string[] configFiles = Directory.GetFiles(UiProjectDirectory(), "appsettings*.json");
        configFiles.ShouldNotBeEmpty();

        foreach (string file in configFiles)
        {
            File.ReadAllText(file).ShouldNotContain(
                DevClientSecret,
                Case.Insensitive,
                $"{Path.GetFileName(file)} must not commit the dev client secret.");
        }
    }

    private static string UiProjectDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "Hexalith.Parties.UI");
            if (File.Exists(Path.Combine(candidate, "Hexalith.Parties.UI.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Hexalith.Parties.UI project directory.");
    }
}
