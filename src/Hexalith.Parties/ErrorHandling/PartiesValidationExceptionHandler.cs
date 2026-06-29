using FluentValidation;

using Hexalith.Commons.Http;
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

        ProblemDetails problemDetails = BoundedProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Validation Failed",
            "https://tools.ietf.org/html/rfc9457#section-3",
            "One or more validation errors occurred.",
            httpContext.Request.Path,
            correlationId);
        problemDetails.Extensions["validationErrors"] = validationException.Errors.Select(e => new
        {
            field = e.PropertyName,
            message = e.ErrorMessage,
        }).ToArray();

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json",
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
