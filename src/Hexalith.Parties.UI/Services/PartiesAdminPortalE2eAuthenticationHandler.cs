using System.Security.Claims;
using System.Text.Encodings.Web;

using Hexalith.Parties.Contracts.Authorization;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.UI.Services;

internal sealed class PartiesAdminPortalE2eAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHttpContextAccessor httpContextAccessor)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "PartiesAdminPortalE2E";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        ClaimsPrincipal principal = PartiesAdminPortalE2eFixture.CreatePrincipal(httpContextAccessor)
            ?? new ClaimsPrincipal();
        principal.AddIdentity(new ClaimsIdentity(
            [
                new Claim("roles", PartiesRoles.Admin),
                new Claim("roles", PartiesRoles.Consumer),
            ],
            authenticationType: "PartiesE2eRouteAuthorization",
            nameType: ClaimTypes.Name,
            roleType: "roles"));

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        string returnUrl = Request.PathBase.Add(Request.Path) + Request.QueryString;
        string location = QueryHelpers.AddQueryString(
            "/authentication/challenge",
            "returnUrl",
            returnUrl);
        Response.Redirect(location);
        return Task.CompletedTask;
    }
}
