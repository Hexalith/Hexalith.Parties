using System.Globalization;

using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Reads real owned SDK authority state for each qualification call; generation stamps grant no workload credentials.</summary>
/// <param name="read">Reads the actual independently selected authority cell through the SDK.</param>
/// <param name="observe">Retains the observed generation and selected negative-control mode.</param>
internal sealed class OwnedRouteCatalogBindingHandler(
    Func<CancellationToken, Task<RouteCatalogActivationState>> read,
    Action<string, long, long?, string, string> observe) : DelegatingHandler
{
    /// <summary>Gets or sets the current, missing or stale generation control.</summary>
    internal string Mode { get; set; } = "current";

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RouteCatalogActivationState state = await read(cancellationToken).ConfigureAwait(false);
        var committed = state.Committed ?? throw new InvalidOperationException("The owned runtime has no committed catalog activation.");
        string binding = RouteCatalogActivationBinding.ComputeDigest(committed);
        if (Mode is not ("current" or "missing" or "stale")) { throw new InvalidOperationException("Unsupported generation control."); }
        request.Headers.Remove(RouteCatalogInvocationHandler.RootHeader);
        request.Headers.Remove(RouteCatalogInvocationHandler.EpochHeader);
        request.Headers.Remove(RouteCatalogInvocationHandler.BindingHeader);
        long? sentEpoch = Mode == "missing" ? null : Mode == "stale" ? checked(state.Epoch - 1) : state.Epoch;
        if (sentEpoch is { } epoch)
        {
            request.Headers.Add(RouteCatalogInvocationHandler.RootHeader, committed.RootDigest);
            request.Headers.Add(RouteCatalogInvocationHandler.EpochHeader, epoch.ToString(CultureInfo.InvariantCulture));
            request.Headers.Add(RouteCatalogInvocationHandler.BindingHeader, binding);
        }
        observe(Mode, state.Epoch, sentEpoch, committed.RootDigest, binding);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
