extern alias apphost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Hexalith.Parties.IntegrationTests.HealthChecks;

using Shouldly;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises the actual resource-command coordinator without starting external infrastructure.</summary>
[Collection("Non-parallel")]
public sealed class PartiesCatalogParticipantStartupTests
{
    /// <summary>Each fixture scope enrolls its actual required UI even when startup itself waits for catalog readiness.</summary>
    /// <param name="scope">The fixture scope represented by the coordinated startup.</param>
    /// <param name="legacyComplete">Whether the ordinary legacy complete profile also requests the UI.</param>
    [Theory]
    [InlineData("ordinary", false)]
    [InlineData("memory", false)]
    [InlineData("complete", true)]
    public async Task RequiredUiStartsThroughActualCommandBeforeReadinessWaitingStartupReturns(string scope, bool legacyComplete)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        var ui = builder.AddResource(new ProjectResource("eventstore-admin-ui")).WithExplicitStart();
        AddParameters(builder);
        var beforeStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int commands = 0;
        ui.WithCommand(KnownResourceCommands.StartCommand, "Start", _ =>
        {
            commands++;
            started.TrySetResult();
            return Task.FromResult(CommandResults.Success());
        });
        DistributedApplication application = builder.Build();
        await using var lifetime = application.ConfigureAwait(true);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await PartiesCatalogParticipantStartup.StartAsync(builder, application, async (app, token) =>
        {
            // Model the actual BeforeStart bootstrap, which adds the role and exact required-host list.
            ui.WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__Role", "aux-client")
                .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__Directory", Path.GetTempPath())
                .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__RequiredHostIds__0", "eventstore")
                .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__RequiredHostIds__1", "eventstore-admin-ui");
            beforeStart.TrySetResult();
            await PublishNotStartedAsync(app, ui.Resource).ConfigureAwait(false);
            await started.Task.WaitAsync(token).ConfigureAwait(false);
        }, beforeStart.Task, legacyComplete, timeout.Token).ConfigureAwait(true);
        commands.ShouldBe(1, scope);
        ui.Resource.Annotations.OfType<ExplicitStartupAnnotation>().ShouldHaveSingleItem();
    }

    /// <summary>A no-op controlled start cannot accidentally run optional UI commands, even with a private modeled parameter set.</summary>
    /// <param name="privateParameters">Whether private model parameters exist.</param>
    /// <param name="fails">Whether the controlled startup fails.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ControlledStartWithoutBeforeStartNeverStartsUi(bool privateParameters, bool fails)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        var ui = builder.AddResource(new ProjectResource("eventstore-admin-ui")).WithExplicitStart();
        if (privateParameters) { AddParameters(builder); }
        int commands = 0;
        ui.WithCommand(KnownResourceCommands.StartCommand, "Start", _ =>
        {
            commands++;
            return Task.FromResult(CommandResults.Success());
        });
        DistributedApplication app = builder.Build();
        await using var lifetime = app.ConfigureAwait(true);
        var beforeStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task Start(DistributedApplication application, CancellationToken cancellationToken) => fails ? Task.FromException(new InvalidOperationException("controlled startup")) : Task.CompletedTask;
        Task run = PartiesCatalogParticipantStartup.StartAsync(builder, app, Start, beforeStart.Task, legacyComplete: true, TestContext.Current.CancellationToken);
        if (fails) { (await Should.ThrowAsync<InvalidOperationException>(() => run).ConfigureAwait(true)).Message.ShouldBe("controlled startup"); }
        else { await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ConfigureAwait(true); }
        commands.ShouldBe(0);
    }

    /// <summary>Missing enrollment and failed actual commands retain the correct refusal instead of claiming a ready participant.</summary>
    /// <param name="required">Whether the real bootstrap lists this UI.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualEnrollmentControlsCommandsAndCommandFailureCancelsReadinessWait(bool required)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        var ui = builder.AddResource(new ProjectResource("eventstore-admin-ui")).WithExplicitStart()
            .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__Role", "aux-client")
            .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__Directory", Path.GetTempPath())
            .WithEnvironment("EventStore__RouteCatalog__DevelopmentBootstrap__RequiredHostIds__0", required ? "eventstore-admin-ui" : "eventstore");
        AddParameters(builder);
        int commands = 0;
        ui.WithCommand(KnownResourceCommands.StartCommand, "Start", _ =>
        {
            commands++;
            return Task.FromResult(CommandResults.Failure("actual command refused"));
        });
        DistributedApplication app = builder.Build();
        await using var lifetime = app.ConfigureAwait(true);
        var beforeStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool canceled = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        Task run = PartiesCatalogParticipantStartup.StartAsync(builder, app, async (application, token) =>
        {
            beforeStart.TrySetResult();
            await PublishNotStartedAsync(application, ui.Resource).ConfigureAwait(false);
            if (required)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { canceled = true; throw; }
            }
        }, beforeStart.Task, legacyComplete: false, timeout.Token);
        if (required)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => run).ConfigureAwait(true)).Message.ShouldContain("start command failed");
            canceled.ShouldBeTrue();
        }
        else { await run.ConfigureAwait(true); }
        commands.ShouldBe(required ? 1 : 0);
    }

    /// <summary>The actual native model retains its explicit UI annotation and running-process waits without readiness health cycles.</summary>
    [Fact]
    public async Task ExistingCatalogHostWaitsHaveNoProjectHealthChecksAndRemainIntact()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<apphost::Projects.Hexalith_Parties_AppHost>(
            ["--operation=run", "--EnableKeycloak=false", "--EnableEventStoreSampleUi=true", "--Parties:Testing:UsePackagedGateway=false"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var lifetime = builder.ConfigureAwait(true);
        ProjectResource ui = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore-admin-ui");
        ui.Annotations.OfType<ExplicitStartupAnnotation>().ShouldHaveSingleItem();
        ui.Annotations.OfType<WaitAnnotation>().Select(wait => wait.Resource.Name).ShouldContain("eventstore-admin");
        foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
        foreach (WaitAnnotation wait in project.Annotations.OfType<WaitAnnotation>().Where(wait => wait.Resource is ProjectResource))
        {
            wait.Resource.Annotations.OfType<HealthCheckAnnotation>().ShouldBeEmpty(project.Name + " waits on " + wait.Resource.Name);
        }
    }

    private static void AddParameters(IDistributedApplicationBuilder builder)
    {
        foreach (string kind in new[] { "app", "dapr" })
        {
            builder.AddParameter("catalog-eventstore-admin-ui-" + kind + "-token", Guid.NewGuid().ToString("N"), secret: true);
        }
    }

    private static Task PublishNotStartedAsync(DistributedApplication app, ProjectResource ui) => app.ResourceNotifications.PublishUpdateAsync(ui, snapshot => snapshot with
    {
        State = new ResourceStateSnapshot(KnownResourceStates.NotStarted, null),
        Commands = [new ResourceCommandSnapshot(KnownResourceCommands.StartCommand, ResourceCommandState.Enabled, "Start", null, null, null, null, null, false)],
    });
}
