namespace Hexalith.Parties.AdminPortal.Services;

public static class AdminPortalQueryBounds
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public static int BoundPage(int page) => Math.Max(1, page);

    public static int BoundPageSize(int pageSize)
        => Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaximumPageSize);
}
