using System.Text.Json;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>
/// Story 1.2 Task 4 (supports AC1/AC2): the dev Keycloak realm import must declare the confidential
/// <c>hexalith-parties-ui</c> client the authorization-code browser flow needs. The pre-existing
/// public clients (<c>hexalith-parties</c>/<c>hexalith-eventstore</c>) are direct-access-grant and
/// cannot perform the code flow, so a missing/misconfigured confidential client silently breaks
/// sign-in. Validated against the committed realm JSON; no running Keycloak required.
/// </summary>
public sealed class PartiesUiKeycloakRealmTests
{
    private const string PartiesUiClientId = "hexalith-parties-ui";

    [Fact]
    public void Realm_DeclaresConfidentialPartiesUiClient_ForAuthorizationCodeFlow()
    {
        JsonElement client = FindPartiesUiClient();

        // Confidential authorization-code client: a server-held secret + standard flow, not the
        // public direct-grant posture of the other clients.
        client.GetProperty("publicClient").GetBoolean().ShouldBeFalse();
        client.GetProperty("standardFlowEnabled").GetBoolean().ShouldBeTrue();
        client.GetProperty("directAccessGrantsEnabled").GetBoolean().ShouldBeFalse();
        client.GetProperty("secret").GetString().ShouldNotBeNullOrWhiteSpace();

        // The redirect URI MUST match the parties-ui https origin (built from the request host),
        // else Keycloak rejects the callback with "Invalid redirect_uri" and sign-in fails.
        client.GetProperty("redirectUris").EnumerateArray()
            .Select(static uri => uri.GetString())
            .ShouldContain("https://localhost:7210/signin-oidc");
    }

    [Fact]
    public void PartiesUiClient_MapsTheEventStoreAudience_ForDownstreamClaims()
    {
        JsonElement client = FindPartiesUiClient();

        // The audience mapper stamps hexalith-eventstore onto the access token so later transport
        // stories' downstream calls are accepted — keep it bundled with the realm client.
        bool hasEventStoreAudience = client.GetProperty("protocolMappers").EnumerateArray()
            .Any(static mapper =>
                mapper.GetProperty("protocolMapper").GetString() == "oidc-audience-mapper"
                && mapper.GetProperty("config").TryGetProperty("included.client.audience", out JsonElement audience)
                && audience.GetString() == "hexalith-eventstore");

        hasEventStoreAudience.ShouldBeTrue(
            "the hexalith-parties-ui client must keep the hexalith-eventstore audience mapper.");
    }

    private static JsonElement FindPartiesUiClient()
    {
        string realmPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Hexalith.Parties.AppHost",
            "KeycloakRealms",
            "hexalith-realm.json");
        File.Exists(realmPath).ShouldBeTrue($"Missing Keycloak realm import at {realmPath}.");

        using JsonDocument realm = JsonDocument.Parse(File.ReadAllText(realmPath));

        JsonElement? match = null;
        foreach (JsonElement client in realm.RootElement.GetProperty("clients").EnumerateArray())
        {
            if (client.GetProperty("clientId").GetString() == PartiesUiClientId)
            {
                // Clone so the element survives the JsonDocument's disposal at method exit.
                match = client.Clone();
                break;
            }
        }

        match.ShouldNotBeNull($"realm must declare the confidential '{PartiesUiClientId}' client.");
        return match.Value;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Parties.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root (Hexalith.Parties.slnx).");
    }
}
