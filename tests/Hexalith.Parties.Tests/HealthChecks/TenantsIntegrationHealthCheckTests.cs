using Hexalith.Parties.Configuration;
using Hexalith.Parties.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.Parties.Tests.HealthChecks;

public sealed class TenantsIntegrationHealthCheckTests
{
    private readonly FakeTenantsReadinessProbe _probe = new();

    private readonly HealthCheckContext _context = new()
    {
        Registration = new HealthCheckRegistration(
            "tenants-integration",
            new FakeHealthCheck(),
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready"]),
    };

    [Fact]
    public async Task CheckHealthAsync_WhenTenantsDisabled_ReturnsHealthyWithoutProbeAsync()
    {
        var sut = new TenantsIntegrationHealthCheck(
            Options.Create(new TenantIntegrationOptions { Enabled = false }),
            _probe);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldNotBeNull().ShouldContain("disabled");
        _probe.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTenantsReady_ReturnsHealthyAsync()
    {
        _probe.Result = true;

        var sut = new TenantsIntegrationHealthCheck(Options.Create(EnabledOptions()), _probe);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldNotBeNull().ShouldContain("tenants");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTenantsUnavailable_ReturnsRegistrationFailureStatusAsync()
    {
        _probe.Result = false;

        var sut = new TenantsIntegrationHealthCheck(Options.Create(EnabledOptions()), _probe);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull().ShouldContain("not ready");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenProbeThrows_ReturnsRegistrationFailureStatusAsync()
    {
        _probe.Exception = new HttpRequestException();

        var sut = new TenantsIntegrationHealthCheck(Options.Create(EnabledOptions()), _probe);

        HealthCheckResult result = await sut.CheckHealthAsync(_context);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldNotBeNull().ShouldContain("HttpRequestException");
        result.Exception.ShouldNotBeNull();
    }

    private static TenantIntegrationOptions EnabledOptions() => new()
    {
        Enabled = true,
        ServiceName = "tenants",
        CommandApiAppId = "parties",
        PubSubName = "pubsub",
        TopicName = "system.tenants.events",
    };

    private sealed class FakeTenantsReadinessProbe : ITenantsReadinessProbe
    {
        public int CallCount { get; private set; }

        public bool Result { get; set; }

        public Exception? Exception { get; set; }

        public Task<bool> IsReadyAsync(string serviceName, CancellationToken cancellationToken)
        {
            serviceName.ShouldBe("tenants");
            CallCount++;

            if (Exception is not null)
            {
                return Task.FromException<bool>(Exception);
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(HealthCheckResult.Healthy());
    }
}
