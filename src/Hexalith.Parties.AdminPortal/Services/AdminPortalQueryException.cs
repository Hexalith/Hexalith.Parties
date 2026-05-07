namespace Hexalith.Parties.AdminPortal.Services;

public sealed class AdminPortalQueryException : Exception
{
    public AdminPortalQueryException(AdminPortalQueryFailureKind kind, int? statusCode = null, string? validationDetail = null, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(DefaultMessage(kind), innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
        ValidationDetail = validationDetail;
        RetryAfter = retryAfter;
    }

    public AdminPortalQueryFailureKind Kind { get; }

    public int? StatusCode { get; }

    public string? ValidationDetail { get; }

    public TimeSpan? RetryAfter { get; }

    private static string DefaultMessage(AdminPortalQueryFailureKind kind)
        => kind switch
        {
            AdminPortalQueryFailureKind.AuthenticationRequired => "Sign-in is required.",
            AdminPortalQueryFailureKind.TenantRequired => "Tenant context is required.",
            AdminPortalQueryFailureKind.Forbidden => "Access is denied.",
            AdminPortalQueryFailureKind.NotFound => "The selected party is unavailable.",
            AdminPortalQueryFailureKind.Gone => "The selected party is erased or no longer inspectable.",
            AdminPortalQueryFailureKind.TransientFailure => "The request could not be completed. Try again.",
            AdminPortalQueryFailureKind.Validation => "The request was rejected as invalid.",
            _ => "The request could not be completed.",
        };
}
