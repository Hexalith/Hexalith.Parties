namespace Hexalith.Parties.CommandApi.Middleware;

public sealed class GdprWarningMiddleware(RequestDelegate next)
{
    private const string GdprWarningHeader = "X-GDPR-Warning";
    private const string GdprWarningMessage =
        "GDPR Notice: This MVP does not include GDPR compliance features "
        + "(crypto-shredding, consent, erasure). Do not store regulated EU personal data. "
        + "See v1.1 roadmap.";

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
