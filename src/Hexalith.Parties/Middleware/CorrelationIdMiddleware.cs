using Hexalith.Commons.Http;

namespace Hexalith.Parties.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    Hexalith.Parties.Security.ICorrelationContextAccessor correlationContextAccessor)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        await HttpCorrelation
            .InvokeAsync(context, correlationContextAccessor, next, HeaderName, HttpContextKey)
            .ConfigureAwait(false);
    }
}
