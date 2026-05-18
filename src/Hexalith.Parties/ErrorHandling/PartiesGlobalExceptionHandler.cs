using Hexalith.Parties.Middleware;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.ErrorHandling;

public sealed class PartiesGlobalExceptionHandler(ILogger<PartiesGlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";

        Exception? authorizationException = FindAuthorizationException(exception);
        if (authorizationException is not null)
        {
            string detail = GetPropertyValue(authorizationException, "Reason")
                ?? "Cross-tenant access denied.";
            string? tenantId = GetPropertyValue(authorizationException, "TenantId");

            logger.LogWarning(
                authorizationException,
                "Authorization denied: CorrelationId={CorrelationId}, Tenant={TenantId}",
                correlationId,
                tenantId ?? "unknown");

            var forbiddenDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Type = "urn:hexalith:parties:error:Forbidden",
                Detail = detail,
                Instance = httpContext.Request.Path,
                Extensions = { ["correlationId"] = correlationId },
            };

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                forbiddenDetails.Extensions["tenantId"] = tenantId;
            }

            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await httpContext.Response.WriteAsJsonAsync(
                forbiddenDetails,
                (System.Text.Json.JsonSerializerOptions?)null,
                "application/problem+json",
                cancellationToken).ConfigureAwait(false);

            return true;
        }

        Exception? dependencyException = FindDependencyException(exception);
        if (dependencyException is not null && !IsClientAbort(httpContext, dependencyException))
        {
            logger.LogError(
                dependencyException,
                "Infrastructure dependency unavailable: CorrelationId={CorrelationId}",
                correlationId);

            var dependencyDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Dependency Unavailable",
                Type = "urn:hexalith:parties:error:DependencyUnavailable",
                Detail = "A required infrastructure dependency is temporarily unavailable. Retry the request after recovery.",
                Instance = httpContext.Request.Path,
                Extensions = { ["correlationId"] = correlationId },
            };

            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await httpContext.Response.WriteAsJsonAsync(
                dependencyDetails,
                (System.Text.Json.JsonSerializerOptions?)null,
                "application/problem+json",
                cancellationToken).ConfigureAwait(false);

            return true;
        }

        logger.LogError(exception, "Unhandled exception: CorrelationId={CorrelationId}", correlationId);

        bool isDevelopment = httpContext.RequestServices
            .GetService<IHostEnvironment>()?.IsDevelopment() == true;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = isDevelopment
                ? $"[{exception.GetType().Name}]"
                : "An unexpected error occurred while processing your request.",
            Instance = httpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json",
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static Exception? FindAuthorizationException(Exception exception)
    {
        const int maxDepth = 10;
        return FindAuthorizationExceptionRecursive(exception, maxDepth);
    }

    private static Exception? FindDependencyException(Exception exception)
    {
        const int maxDepth = 10;
        return FindDependencyExceptionRecursive(exception, maxDepth);
    }

    private static Exception? FindAuthorizationExceptionRecursive(Exception? exception, int remainingDepth)
    {
        if (exception is null || remainingDepth <= 0)
        {
            return null;
        }

        if (string.Equals(exception.GetType().Name, "CommandAuthorizationException", StringComparison.Ordinal))
        {
            return exception;
        }

        if (exception is AggregateException aggregateException)
        {
            foreach (Exception inner in aggregateException.InnerExceptions)
            {
                Exception? found = FindAuthorizationExceptionRecursive(inner, remainingDepth - 1);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return FindAuthorizationExceptionRecursive(exception.InnerException, remainingDepth - 1);
    }

    private static Exception? FindDependencyExceptionRecursive(Exception? exception, int remainingDepth)
    {
        if (exception is null || remainingDepth <= 0)
        {
            return null;
        }

        if (exception is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return exception;
        }

        if (exception is AggregateException aggregateException)
        {
            foreach (Exception inner in aggregateException.InnerExceptions)
            {
                Exception? found = FindDependencyExceptionRecursive(inner, remainingDepth - 1);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return FindDependencyExceptionRecursive(exception.InnerException, remainingDepth - 1);
    }

    private static bool IsClientAbort(HttpContext httpContext, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        return httpContext.RequestAborted.IsCancellationRequested
            && exception is OperationCanceledException;
    }

    private static string? GetPropertyValue(Exception exception, string propertyName)
    {
        object? value = exception.GetType().GetProperty(propertyName)?.GetValue(exception);
        return value as string;
    }
}
