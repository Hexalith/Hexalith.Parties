using System.Text.Json;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>
/// Story 1.3 Task 6 (supports the optional live AC1/AC2 flow): the dev Keycloak realm must provision
/// the <c>Admin</c>/<c>TenantOwner</c>/<c>Consumer</c> realm roles, assign them to the seed users, and
/// emit realm roles flat under a <c>roles</c> claim on the <c>hexalith-parties-ui</c> client so the
/// host's <c>RoleClaimType = "roles"</c> mapping lands them in <c>ClaimsPrincipal</c>. The automated
/// bUnit/DI tests remain the binding proof of the ACs; this pins the realm wiring the live flow needs.
/// Validated against the committed realm JSON; no running Keycloak required.
/// </summary>
public sealed class PartiesUiRealmRolesTests
{
    private const string PartiesUiClientId = "hexalith-parties-ui";

    [Fact]
    public void Realm_DeclaresAdminTenantOwnerAndConsumerRealmRoles()
    {
        using JsonDocument realm = LoadRealm();

        List<string> roleNames = realm.RootElement
            .GetProperty("roles")
            .GetProperty("realm")
            .EnumerateArray()
            .Select(static role => role.GetProperty("name").GetString())
            .Where(static name => name is not null)
            .Select(static name => name!)
            .ToList();

        roleNames.ShouldContain("Admin");
        roleNames.ShouldContain("TenantOwner");
        roleNames.ShouldContain("Consumer");
    }

    [Fact]
    public void SeedUsers_CarryTheirLandingRoles()
    {
        using JsonDocument realm = LoadRealm();

        UserRealmRoles(realm, "admin-user").ShouldContain("Admin");
        UserRealmRoles(realm, "readonly-user").ShouldContain("Consumer");
    }

    [Fact]
    public void PartiesUiClient_EmitsRealmRolesFlatUnderTheRolesClaim()
    {
        using JsonDocument realm = LoadRealm();

        bool hasFlatRolesMapper = FindPartiesUiClient(realm)
            .GetProperty("protocolMappers")
            .EnumerateArray()
            .Any(static mapper =>
                mapper.GetProperty("protocolMapper").GetString() == "oidc-usermodel-realm-role-mapper"
                && mapper.GetProperty("config").TryGetProperty("claim.name", out JsonElement claim)
                && claim.GetString() == "roles");

        hasFlatRolesMapper.ShouldBeTrue(
            "the hexalith-parties-ui client must emit realm roles flat under the 'roles' claim so "
            + "RoleClaimType mapping lands them in the signed-in principal.");
    }

    private static List<string> UserRealmRoles(JsonDocument realm, string username)
    {
        foreach (JsonElement user in realm.RootElement.GetProperty("users").EnumerateArray())
        {
            if (user.GetProperty("username").GetString() == username)
            {
                return user.TryGetProperty("realmRoles", out JsonElement roles)
                    ? roles.EnumerateArray().Select(static role => role.GetString()!).ToList()
                    : [];
            }
        }

        throw new InvalidOperationException($"User '{username}' not found in realm.");
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
