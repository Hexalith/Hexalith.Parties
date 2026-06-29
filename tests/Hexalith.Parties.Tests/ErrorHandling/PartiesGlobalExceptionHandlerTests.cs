using System.Text.Json;

using Hexalith.Parties.ErrorHandling;
using Hexalith.Parties.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        using JsonDocument payload = await JsonDocument.ParseAsync(context.Response.Body);
        payload.RootElement.GetProperty("title").GetString().ShouldBe("Dependency Unavailable");
    }

    [Fact]
    public async Task TryHandleAsync_DevelopmentUnhandledException_DoesNotEchoExceptionMessageAsync()
    {
        var handler = new PartiesGlobalExceptionHandler(NullLogger<PartiesGlobalExceptionHandler>.Instance);
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment { EnvironmentName = Environments.Development });

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();

        bool handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("synthetic-personal-data-sentinel"),
            CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);

        context.Response.Body.Position = 0;
        using JsonDocument payload = await JsonDocument.ParseAsync(context.Response.Body);
        JsonElement detail = payload.RootElement.GetProperty("detail");
        detail.GetString().ShouldBe("[InvalidOperationException]");
    }

    [Fact]
    public async Task TryHandleAsync_ProductionUnhandledException_ReturnsBoundedProblemDetailsWithCorrelationAsync()
    {
        var handler = new PartiesGlobalExceptionHandler(NullLogger<PartiesGlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/process";
        context.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-unhandled";
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        context.Response.Body = new MemoryStream();

        bool handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("synthetic-personal-data-sentinel"),
            CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.ShouldStartWith("application/problem+json");

        context.Response.Body.Position = 0;
        using JsonDocument payload = await JsonDocument.ParseAsync(context.Response.Body);
        JsonElement root = payload.RootElement;
        root.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status500InternalServerError);
        root.GetProperty("title").GetString().ShouldBe("Internal Server Error");
        root.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred while processing your request.");
        root.GetProperty("instance").GetString().ShouldBe("/process");
        root.GetProperty("correlationId").GetString().ShouldBe("corr-unhandled");
        root.GetRawText().ShouldNotContain("synthetic-personal-data-sentinel", Case.Sensitive);
    }

    [Fact]
    public async Task TryHandleAsync_NestedAuthorizationException_ReturnsForbiddenProblemDetailsAsync()
    {
        var handler = new PartiesGlobalExceptionHandler(NullLogger<PartiesGlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/process";
        context.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-forbidden";
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        var exception = new AggregateException(
            new CommandAuthorizationException("Cross-tenant access denied.", "tenant-a"));

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);

        context.Response.Body.Position = 0;
        using JsonDocument payload = await JsonDocument.ParseAsync(context.Response.Body);
        JsonElement root = payload.RootElement;
        root.GetProperty("status").GetInt32().ShouldBe(StatusCodes.Status403Forbidden);
        root.GetProperty("title").GetString().ShouldBe("Forbidden");
        root.GetProperty("detail").GetString().ShouldBe("Cross-tenant access denied.");
        root.GetProperty("correlationId").GetString().ShouldBe("corr-forbidden");
        root.GetProperty("tenantId").GetString().ShouldBe("tenant-a");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = nameof(PartiesGlobalExceptionHandlerTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class CommandAuthorizationException(string reason, string tenantId) : Exception(reason)
    {
        public string Reason { get; } = reason;

        public string TenantId { get; } = tenantId;
    }
}
