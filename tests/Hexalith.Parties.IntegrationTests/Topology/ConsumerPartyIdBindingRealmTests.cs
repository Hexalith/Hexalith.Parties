using System.Text.Json;

using Hexalith.Parties.Contracts.Authorization;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>
/// Story 4.1 pins the accepted Consumer identity binding boundary: admin-link provisioning writes a
/// verified IdP user attribute, and the UI receives exactly one <c>party_id</c> claim. These tests
/// validate the committed Keycloak realm import without requiring a running Keycloak instance.
/// </summary>
public sealed class ConsumerPartyIdBindingRealmTests
{
    private const string PartiesUiClientId = "hexalith-parties-ui";

    [Fact]
    public void PartiesUiClient_EmitsPartyIdClaimFromSingleValuedUserAttribute()
    {
        using JsonDocument realm = LoadRealm();

        JsonElement mapper = FindProtocolMapper(realm, "party-id-mapper");
        JsonElement config = mapper.GetProperty("config");

        mapper.GetProperty("protocol").GetString().ShouldBe("openid-connect");
        mapper.GetProperty("protocolMapper").GetString().ShouldBe("oidc-usermodel-attribute-mapper");
        config.GetProperty("user.attribute").GetString().ShouldBe("party_id");
        config.GetProperty("claim.name").GetString().ShouldBe("party_id");
        config.GetProperty("jsonType.label").GetString().ShouldBe("String");
        config.GetProperty("multivalued").GetString().ShouldBe("false");
        config.GetProperty("id.token.claim").GetString().ShouldBe("true");
        config.GetProperty("access.token.claim").GetString().ShouldBe("true");
        config.GetProperty("userinfo.token.claim").GetString().ShouldBe("true");
    }

    [Fact]
    public void BoundConsumerSeedUser_CarriesExactlyOneSyntheticPartyIdAttribute()
    {
        using JsonDocument realm = LoadRealm();

        JsonElement user = FindUser(realm, "readonly-user");

        UserRealmRoles(user).ShouldContain(PartiesRoles.Consumer);
        UserAttributes(user, "party_id").ShouldBe(["party-readonly-001"]);
        UserAttributes(user, "tenants").ShouldContain("tenant-a");
    }

    [Fact]
    public void ConsumerPartyIdSeedAttributes_AreSingleValuedAndNonEmpty()
    {
        using JsonDocument realm = LoadRealm();

        foreach (JsonElement user in realm.RootElement.GetProperty("users").EnumerateArray())
        {
            if (!UserRealmRoles(user).Contains(PartiesRoles.Consumer, StringComparer.Ordinal))
            {
                continue;
            }

            List<string> partyIds = UserAttributes(user, "party_id");
            if (partyIds.Count == 0)
            {
                continue;
            }

            partyIds.Count.ShouldBe(1, $"Consumer seed user '{Username(user)}' must not emit ambiguous party_id claims.");
            partyIds[0].ShouldNotBeNullOrWhiteSpace($"Consumer seed user '{Username(user)}' must not emit an empty party_id claim.");
        }
    }

    private static JsonElement FindProtocolMapper(JsonDocument realm, string mapperName)
    {
        foreach (JsonElement mapper in FindPartiesUiClient(realm).GetProperty("protocolMappers").EnumerateArray())
        {
            if (mapper.GetProperty("name").GetString() == mapperName)
            {
                return mapper;
            }
        }

        throw new InvalidOperationException($"Parties UI client mapper '{mapperName}' was not found.");
    }

    private static JsonElement FindPartiesUiClient(JsonDocument realm)
    {
        foreach (JsonElement client in realm.RootElement.GetProperty("clients").EnumerateArray())
        {
            if (client.GetProperty("clientId").GetString() == PartiesUiClientId)
            {
                return client;
            }
        }

        throw new InvalidOperationException($"realm must declare the '{PartiesUiClientId}' client.");
    }

    private static JsonElement FindUser(JsonDocument realm, string username)
    {
        foreach (JsonElement user in realm.RootElement.GetProperty("users").EnumerateArray())
        {
            if (Username(user) == username)
            {
                return user;
            }
        }

        throw new InvalidOperationException($"User '{username}' not found in realm.");
    }

    private static List<string> UserRealmRoles(JsonElement user)
        => user.TryGetProperty("realmRoles", out JsonElement roles)
            ? roles.EnumerateArray().Select(static role => role.GetString()!).ToList()
            : [];

    private static List<string> UserAttributes(JsonElement user, string attributeName)
    {
        if (!user.TryGetProperty("attributes", out JsonElement attributes)
            || !attributes.TryGetProperty(attributeName, out JsonElement values))
        {
            return [];
        }

        return values.EnumerateArray().Select(static value => value.GetString() ?? string.Empty).ToList();
    }

    private static string? Username(JsonElement user)
        => user.GetProperty("username").GetString();

    private static JsonDocument LoadRealm()
    {
        string realmPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Hexalith.Parties.AppHost",
            "KeycloakRealms",
            "hexalith-realm.json");
        File.Exists(realmPath).ShouldBeTrue($"Missing Keycloak realm import at {realmPath}.");
        return JsonDocument.Parse(File.ReadAllText(realmPath));
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
