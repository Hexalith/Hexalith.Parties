using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Parties.HealthChecks;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Projections.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class ProjectionActorsHealthCheckTests
{
    private readonly IActorProxyFactory _actorProxyFactory = Substitute.For<IActorProxyFactory>();
    private readonly IPartyIndexProjectionActor _indexActor = Substitute.For<IPartyIndexProjectionActor>();
    private readonly IPartyDetailProjectionActor _detailActor = Substitute.For<IPartyDetailProjectionActor>();
    private readonly HealthCheckContext _context = new()
    {
        Registration = new HealthCheckRegistration(
            "projection-actors",
            Substitute.For<IHealthCheck>(),
            failureStatus: HealthStatus.Degraded,
            tags: []),
    };

    public ProjectionActorsHealthCheckTests()
    {
        _actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>())
            .Returns(_indexActor);

        _actorProxyFactory.CreateActorProxy<IPartyDetailProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>())
            .Returns(_detailActor);
    }

    [Fact]
    public async Task CheckHealthAsync_ProjectionActorsResponsive_ReturnsHealthyAsync()
    {
        _indexActor.PingAsync().Returns(Task.FromResult(true));
        _indexActor.IsRebuildingAsync().Returns(Task.FromResult(false));
        _detailActor.PingAsync().Returns(Task.FromResult(true));
        _detailActor.IsRebuildingAsync().Returns(Task.FromResult(false));

        var sut = new ProjectionActorsHealthCheck(_actorProxyFactory, NullLogger<ProjectionActorsHealthCheck>.Instance);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldBe("Projection actors are responsive.");
    }

    [Fact]
    public async Task CheckHealthAsync_IndexActorThrows_ReturnsDegradedAsync()
    {
        _indexActor.PingAsync().ThrowsAsync<HttpRequestException>();

        var sut = new ProjectionActorsHealthCheck(_actorProxyFactory, NullLogger<ProjectionActorsHealthCheck>.Instance);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldBe("Projection actor health check failed.");
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_IndexActorRebuilding_ReturnsDegradedWithBoundedDescriptionAsync()
    {
        _indexActor.PingAsync().Returns(Task.FromResult(true));
        _indexActor.IsRebuildingAsync().Returns(Task.FromResult(true));
        _detailActor.PingAsync().Returns(Task.FromResult(true));
        _detailActor.IsRebuildingAsync().Returns(Task.FromResult(false));

        var sut = new ProjectionActorsHealthCheck(_actorProxyFactory, NullLogger<ProjectionActorsHealthCheck>.Instance);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldBe("Projection actors are rebuilding.");
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_DetailActorRebuilding_ReturnsDegradedWithBoundedDescriptionAsync()
    {
        _indexActor.PingAsync().Returns(Task.FromResult(true));
        _indexActor.IsRebuildingAsync().Returns(Task.FromResult(false));
        _detailActor.PingAsync().Returns(Task.FromResult(true));
        _detailActor.IsRebuildingAsync().Returns(Task.FromResult(true));

        var sut = new ProjectionActorsHealthCheck(_actorProxyFactory, NullLogger<ProjectionActorsHealthCheck>.Instance);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldBe("Projection actors are rebuilding.");
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_ProbeTimeout_ReturnsFailureStatusWithoutExceptionLeakAsync()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var sut = new ProjectionActorsHealthCheck(_actorProxyFactory, NullLogger<ProjectionActorsHealthCheck>.Instance);

        HealthCheckResult result = await sut.CheckHealthAsync(_context, cts.Token);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldBe("Projection actor health check canceled or timed out.");
        result.Exception.ShouldBeNull();
    }
}
