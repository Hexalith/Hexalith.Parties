using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Client.Paging;

internal static class PartiesPagedResultAdapter
{
    public static Hexalith.Commons.Http.PagedResult<T> ToCommonsPage<T>(PagedResult<T> page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new Hexalith.Commons.Http.PagedResult<T>
        {
            Items = page.Items ?? [],
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    public static PagedResult<T> ToPartiesPage<T>(
        Hexalith.Commons.Http.PagedResult<T> page,
        ProjectionFreshnessMetadata? freshness)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new PagedResult<T>
        {
            Items = page.Items ?? [],
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
            Freshness = freshness,
        };
    }

    public static PagedResult<T> Normalize<T>(PagedResult<T> page)
        => ToPartiesPage(ToCommonsPage(page), page.Freshness);
}
