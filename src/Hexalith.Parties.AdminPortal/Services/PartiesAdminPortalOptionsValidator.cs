using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.Parties.Client.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.AdminPortal.Services;

internal sealed class PartiesAdminPortalOptionsValidator(IServiceScopeFactory scopeFactory)
    : IValidateOptions<PartiesAdminPortalOptions>
{
    public ValidateOptionsResult Validate(string? name, PartiesAdminPortalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        using IServiceScope scope = scopeFactory.CreateScope();
        bool hasTypedClient = scope.ServiceProvider.GetService<IPartiesQueryClient>() is not null;
        bool hasQueryService = scope.ServiceProvider.GetService<IQueryService>() is not null;

        if (!hasTypedClient && !hasQueryService)
        {
            return ValidateOptionsResult.Fail(
                "AdminPortal needs either an IPartiesQueryClient (typed) or an IQueryService (FrontComposer fallback) registration. Configure one before calling AddHexalithPartiesAdminPortal.");
        }

        if (!hasTypedClient && hasQueryService)
        {
            List<string> missing = [];
            if (string.IsNullOrWhiteSpace(options.ListProjectionType))
            {
                missing.Add(nameof(options.ListProjectionType));
            }

            if (string.IsNullOrWhiteSpace(options.ListQueryType))
            {
                missing.Add(nameof(options.ListQueryType));
            }

            if (string.IsNullOrWhiteSpace(options.SearchProjectionType))
            {
                missing.Add(nameof(options.SearchProjectionType));
            }

            if (string.IsNullOrWhiteSpace(options.SearchQueryType))
            {
                missing.Add(nameof(options.SearchQueryType));
            }

            if (string.IsNullOrWhiteSpace(options.DetailProjectionType))
            {
                missing.Add(nameof(options.DetailProjectionType));
            }

            if (string.IsNullOrWhiteSpace(options.DetailQueryType))
            {
                missing.Add(nameof(options.DetailQueryType));
            }

            if (missing.Count > 0)
            {
                return ValidateOptionsResult.Fail(
                    $"Without IPartiesQueryClient, the IQueryService fallback requires the following options to be set on PartiesAdminPortalOptions: {string.Join(", ", missing)}.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
