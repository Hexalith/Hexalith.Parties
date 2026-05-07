namespace Hexalith.Parties.Middleware;

public sealed class GdprWarningMiddleware(RequestDelegate next)
{
    private const string GdprWarningHeader = "X-GDPR-Warning";
    private const string GdprWarningMessage =
        "GDPR Notice: Personal-data encryption at rest is enabled for protected fields, but consent management "
        + "and erasure verification are not complete. Treat this service as partially compliant only.";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[GdprWarningHeader] = GdprWarningMessage;
            return Task.CompletedTask;
        });

        await next(context).ConfigureAwait(false);
    }
}
