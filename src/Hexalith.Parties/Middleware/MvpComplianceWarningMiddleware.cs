using Hexalith.Parties.Compliance;

namespace Hexalith.Parties.Middleware;

public sealed class MvpComplianceWarningMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!configuration.GetValue<bool>(MvpComplianceWarning.ActivationConfigurationKey))
        {
            context.Response.OnStarting(static state =>
            {
                HttpContext httpContext = (HttpContext)state;
                if (!httpContext.Response.Headers.ContainsKey(MvpComplianceWarning.HeaderName))
                {
                    httpContext.Response.Headers[MvpComplianceWarning.HeaderName] = MvpComplianceWarning.Message;
                }

                return Task.CompletedTask;
            }, context);
        }

        await next(context).ConfigureAwait(false);
    }
}
