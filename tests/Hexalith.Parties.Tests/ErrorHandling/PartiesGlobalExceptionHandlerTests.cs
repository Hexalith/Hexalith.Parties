using System.Text.Json;

using Hexalith.Parties.ErrorHandling;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.Parties.Tests.ErrorHandling;

public sealed class PartiesGlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_RequestAbortedTaskCanceled_DoesNotReportDependencyUnavailableAsync()
    {
        var handler = new PartiesGlobalExceptionHandler(NullLogger<PartiesGlobalExceptionHandler>.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = new DefaultHttpContext
        {
            RequestAborted = cts.Token,
        };
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();

        bool handled = await handler.TryHandleAsync(
            context,
            new TaskCanceledException("client disconnected"),
            CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_DependencyTimeout_ReturnsDependencyUnavailableAsync()
    {
        var handler = new PartiesGlobalExceptionHandler(NullLogger<PartiesGlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();

        bool handled = await handler.TryHandleAsync(
            context,
            new TimeoutException("dependency timed out"),
            CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);

        context.Response.Body.Position = 0;
        JsonDocument payload = await JsonDocument.ParseAsync(context.Response.Body);
        payload.RootElement.GetProperty("title").GetString().ShouldBe("Dependency Unavailable");
    }
}