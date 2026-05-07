using FluentValidation;

using Hexalith.Parties.Middleware;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.Parties.ErrorHandling;

public sealed class PartiesValidationExceptionHandler(ILogger<PartiesValidationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not ValidationException validationException)
        {
            return false;
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString() ?? "unknown";

        logger.LogWarning(
            "Validation failed: CorrelationId={CorrelationId}, Errors={ErrorCount}",
            correlationId,
            validationException.Errors.Count());

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = "One or more validation errors occurred.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["validationErrors"] = validationException.Errors.Select(e => new
                {
                    field = e.PropertyName,
                    message = e.ErrorMessage,
                }).ToArray(),
            },
        };

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json",
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
