using Hexalith.Parties.Middleware;
using Hexalith.Parties.Security;

using Microsoft.AspNetCore.Http;

using Shouldly;

namespace Hexalith.Parties.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ValidGuidHeader_PropagatesHeaderAndRestoresAmbientValueAsync()
    {
        const string previous = "previous-correlation";
        const string incoming = "7ae4136f-c16a-4414-878f-71c0b54e3f91";
        var accessor = new CorrelationContextAccessor { CorrelationId = previous };
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming;
        string? observed = null;
        var middleware = new CorrelationIdMiddleware(
            _ =>
            {
                observed = accessor.CorrelationId;
                return Task.CompletedTask;
            },
            accessor);

        await middleware.InvokeAsync(context);

        observed.ShouldBe(incoming);
        context.Items[CorrelationIdMiddleware.HttpContextKey].ShouldBe(incoming);
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().ShouldBe(incoming);
        accessor.CorrelationId.ShouldBe(previous);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("first,second")]
    public async Task InvokeAsync_InvalidHeader_GeneratesGuidFallbackAsync(string incoming)
    {
        var accessor = new CorrelationContextAccessor();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, accessor);

        await middleware.InvokeAsync(context);

        string generated = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Guid.TryParse(generated, out _).ShouldBeTrue();
        generated.ShouldNotBe(incoming);
        context.Items[CorrelationIdMiddleware.HttpContextKey].ShouldBe(generated);
        accessor.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_RestoresPreviousAmbientValueAsync()
    {
        const string previous = "previous-correlation";
        const string incoming = "16a1acc7-5706-4c87-99de-3d7b8636f454";
        var accessor = new CorrelationContextAccessor { CorrelationId = previous };
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming;
        var middleware = new CorrelationIdMiddleware(
            _ => throw new InvalidOperationException("downstream failure"),
            accessor);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));

        exception.Message.ShouldBe("downstream failure");
        context.Items[CorrelationIdMiddleware.HttpContextKey].ShouldBe(incoming);
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().ShouldBe(incoming);
        accessor.CorrelationId.ShouldBe(previous);
    }
}
