using Hexalith.Parties.Security;

namespace Hexalith.Parties.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ICorrelationContextAccessor correlationContextAccessor)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId;
        if (context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues headerValue)
            && Guid.TryParse(headerValue.ToString(), out _))
        {
            correlationId = headerValue.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items[HttpContextKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        string? previousCorrelationId = correlationContextAccessor.CorrelationId;
        correlationContextAccessor.CorrelationId = correlationId;

        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            correlationContextAccessor.CorrelationId = previousCorrelationId;
        }
    }
}
