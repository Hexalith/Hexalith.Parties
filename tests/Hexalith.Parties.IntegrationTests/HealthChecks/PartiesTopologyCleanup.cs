namespace Hexalith.Parties.IntegrationTests.HealthChecks;

/// <summary>Orders application quiescence before removal of fixture-owned infrastructure.</summary>
internal static class PartiesTopologyCleanup
{
    /// <summary>Stops the topology, disposes its session resources, then cleans up the scoped control plane.</summary>
    /// <param name="stopApplication">Stops all application workers and sidecars.</param>
    /// <param name="disposeApplication">Disposes the application and its owned Redis container.</param>
    /// <param name="disposeBuilder">Disposes any partial application model.</param>
    /// <param name="cleanupControlPlane">Removes only this fixture's control plane and trust bundle.</param>
    /// <param name="disposeBootstrapHost">Disposes the optional tenant bootstrap host before topology cleanup.</param>
    internal static async Task RunAsync(
        Func<Task> stopApplication,
        Func<Task> disposeApplication,
        Func<Task> disposeBuilder,
        Func<Task> cleanupControlPlane,
        Func<Task>? disposeBootstrapHost = null)
    {
        Exception? bootstrapFailure = null;
        try
        {
            try
            {
                if (disposeBootstrapHost is not null)
                {
                    await disposeBootstrapHost().ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                bootstrapFailure = exception;
                throw;
            }
        }
        finally
        {
            try
            {
                await CleanupTopologyAsync(stopApplication, disposeApplication, disposeBuilder, cleanupControlPlane).ConfigureAwait(false);
            }
            catch (Exception topologyFailure) when (bootstrapFailure is not null)
            {
                throw new AggregateException(bootstrapFailure, topologyFailure);
            }
        }
    }

    private static async Task CleanupTopologyAsync(
        Func<Task> stopApplication,
        Func<Task> disposeApplication,
        Func<Task> disposeBuilder,
        Func<Task> cleanupControlPlane)
    {
        bool quiesced = false;
        try
        {
            try
            {
                await stopApplication().ConfigureAwait(false);
                quiesced = true;
            }
            finally
            {
                await disposeApplication().ConfigureAwait(false);
                quiesced = true;
            }
        }
        finally
        {
            try
            {
                await disposeBuilder().ConfigureAwait(false);
            }
            finally
            {
                if (quiesced)
                {
                    await cleanupControlPlane().ConfigureAwait(false);
                }
            }
        }
    }
}
