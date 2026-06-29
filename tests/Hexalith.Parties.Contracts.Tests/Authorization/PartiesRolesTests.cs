using Hexalith.Parties.Contracts.Authorization;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Authorization;

public sealed class PartiesRolesTests
{
    [Fact]
    public void PolicyNames_MatchExistingAuthorizationContracts()
    {
        PartiesRoles.AdminPolicy.ShouldBe("Admin");
        PartiesRoles.ConsumerPolicy.ShouldBe("Consumer");
    }

    [Fact]
    public void BaseRoleArrays_ExposeExistingRoleNamesOnly()
    {
        PartiesRoles.AdminRoleNames.ShouldBe(["Admin", "admin", "Administrator", "administrator"], ignoreOrder: false);
        PartiesRoles.ConsumerRoleNames.ShouldBe(["Consumer", "consumer"], ignoreOrder: false);
    }

    [Fact]
    public void TenantOwnerAliases_AreComposableOutsideTheBaseAdminArray()
    {
        PartiesRoles.TenantOwnerRoleNames.ShouldBe(["TenantOwner", "tenantowner"], ignoreOrder: false);
        PartiesRoles.AdminRoleNames.ShouldNotContain(PartiesRoles.TenantOwner);
        PartiesRoles.AdminRoleNames.ShouldNotContain(PartiesRoles.TenantOwnerLower);
    }
}
