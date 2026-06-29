using System.Reflection;

using Hexalith.Parties.Contracts.Authorization;
using Hexalith.Parties.ConsumerPortal.Components;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Parties.ConsumerPortal.Tests.Components;

public sealed class ConsumerPortalAuthorizationTests
{
    [Theory]
    [InlineData(typeof(MyProfilePage), "/me")]
    [InlineData(typeof(EditMyProfilePage), "/me/edit")]
    [InlineData(typeof(MyConsentPage), "/me/consent")]
    [InlineData(typeof(MyPrivacyPage), "/me/privacy")]
    public void ConsumerPortalRouteShell_DeclaresRoute_AndRequiresConsumerPolicy(
        Type component,
        string expectedRoute)
    {
        component
            .GetCustomAttributes<RouteAttribute>(inherit: false)
            .Select(static route => route.Template)
            .ShouldContain(expectedRoute);

        AuthorizeAttribute authorize = component
            .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .ShouldHaveSingleItem();

        authorize.Policy.ShouldBe(PartiesRoles.ConsumerPolicy);
    }
}
