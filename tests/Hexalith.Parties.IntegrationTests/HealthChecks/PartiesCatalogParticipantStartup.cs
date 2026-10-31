using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Starts an explicitly selected private catalog participant through its actual Aspire resource command.</summary>
internal static class PartiesCatalogParticipantStartup
{
    private const string Ui = "eventstore-admin-ui";
    private const string Prefix = "EventStore__RouteCatalog__DevelopmentBootstrap__";

    /// <summary>Recognizes actual private bootstrap-owned UI parameters before child environment preparation.</summary>
    internal static bool HasModeledAdminUi(IEnumerable<IResource> resources)
    {
        IResource[] model = resources.ToArray();
        return model.OfType<ProjectResource>().Any(resource => resource.Name == Ui)
            && new[] { "app", "dapr" }.All(kind => model.OfType<ParameterResource>()
                .Any(parameter => parameter.Name == "catalog-" + Ui + "-" + kind + "-token" && parameter.Secret));
    }

    /// <summary>Runs the real startup and required-UI observer concurrently so a readiness-waiting startup cannot block catalog enrollment.</summary>
    internal static async Task StartAsync(IDistributedApplicationBuilder builder, DistributedApplication application,
        Func<DistributedApplication, CancellationToken, Task> start, Task beforeStart, bool legacyComplete, CancellationToken cancellationToken)
    {
        if (!HasModeledAdminUi(builder.Resources) && !legacyComplete)
        {
            await start(application, cancellationToken).ConfigureAwait(false);
            return;
        }
        using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task observer = StartRequiredUiAsync(builder, application, beforeStart, legacyComplete, lifetime.Token);
        Task startup = Task.CompletedTask;
        try
        {
            startup = start(application, lifetime.Token);
            Task first = await Task.WhenAny(startup, observer).ConfigureAwait(false);
            if (ReferenceEquals(first, observer)) { await observer.ConfigureAwait(false); }
            await startup.ConfigureAwait(false);
            // Controlled no-op startup never emitted BeforeStart and must not start optional resources.
            if (!beforeStart.IsCompletedSuccessfully) { await lifetime.CancelAsync().ConfigureAwait(false); }
            try { await observer.ConfigureAwait(false); }
            catch (OperationCanceledException) when (!beforeStart.IsCompletedSuccessfully && lifetime.IsCancellationRequested) { }
        }
        catch
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
            // Both operations use the same owned cancellation lifetime. Keep the original startup/command diagnostic.
            try { await Task.WhenAll(startup, observer).WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); }
            catch (Exception) { }
            throw;
        }
    }

    private static async Task StartRequiredUiAsync(IDistributedApplicationBuilder builder, DistributedApplication application,
        Task beforeStart, bool legacyComplete, CancellationToken cancellationToken)
    {
        await beforeStart.WaitAsync(cancellationToken).ConfigureAwait(false);
        // DCP publishes NotStarted after BeforeStart participants have completed model preparation.
        await application.ResourceNotifications.WaitForResourceAsync(Ui, KnownResourceStates.NotStarted, cancellationToken).ConfigureAwait(false);
        ProjectResource ui = application.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<ProjectResource>()
            .Single(resource => resource.Name == Ui);
        EnvironmentCallbackContext environment = new(builder.ExecutionContext, ui, new Dictionary<string, object>(), cancellationToken);
        foreach (EnvironmentCallbackAnnotation annotation in ui.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(environment).ConfigureAwait(false);
        }
        bool required = environment.EnvironmentVariables.GetValueOrDefault(Prefix + "Role") is "aux-client"
            && environment.EnvironmentVariables.GetValueOrDefault(Prefix + "Directory") is string directory && Path.IsPathFullyQualified(directory)
            && environment.EnvironmentVariables.Any(item => item.Key.StartsWith(Prefix + "RequiredHostIds__", StringComparison.Ordinal)
                && item.Value is string host && host == Ui);
        if (!required && !legacyComplete) { return; }
        if (!ui.Annotations.OfType<ExplicitStartupAnnotation>().Any())
        {
            throw new InvalidOperationException("The selected EventStore admin UI lost its explicit-start annotation.");
        }
        ExecuteCommandResult result = await application.Services.GetRequiredService<ResourceCommandService>()
            .ExecuteCommandAsync(Ui, KnownResourceCommands.StartCommand, cancellationToken).ConfigureAwait(false);
        if (!result.Success) { throw new InvalidOperationException("The explicit EventStore admin UI start command failed."); }
    }
}
