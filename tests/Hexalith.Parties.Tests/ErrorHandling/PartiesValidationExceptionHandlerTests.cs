using System.Text.Json;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.Parties.ErrorHandling;
using Hexalith.Parties.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.Tests.ErrorHandling;

public sealed class PartiesValidationExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ValidationException_ReturnsBoundedProblemDetailsWithCorrelationAsync()
    {
        var handler = new PartiesValidationExceptionHandler(
            NullLogger<PartiesValidationExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/process";
        context.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-validation";
        context.Response.Body = new MemoryStream();
        var exception = new ValidationException(
        [
            new ValidationFailure("PartyId", "A party identifier is required.")
            {
                AttemptedValue = "party-secret-001",
            },
        ]);

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldStartWith("application/problem+json");

        context.Response.Body.Position = 0;
        using JsonDocument payload = await JsonDocument.ParseAsync(context.Response.Body);
        JsonElement root = payload.RootElement;
        root.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status400BadRequest);
        root.GetProperty("title").GetString().ShouldBe("Validation Failed");
        root.GetProperty("detail").GetString().ShouldBe("One or more validation errors occurred.");
        root.GetProperty("instance").GetString().ShouldBe("/process");
        root.GetProperty("correlationId").GetString().ShouldBe("corr-validation");

        JsonElement validationError = root.GetProperty("validationErrors")[0];
        validationError.GetProperty("field").GetString().ShouldBe("PartyId");
        validationError.GetProperty("message").GetString().ShouldBe("A party identifier is required.");
        root.GetRawText().ShouldNotContain("party-secret-001", Case.Sensitive);
    }

    [Fact]
    public async Task TryHandleAsync_NonValidationException_ReturnsFalseAsync()
    {
        var handler = new PartiesValidationExceptionHandler(
            NullLogger<PartiesValidationExceptionHandler>.Instance);
        var context = new DefaultHttpContext();

        bool handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("not validation"),
            CancellationToken.None);

        handled.ShouldBeFalse();
    }
}
