using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.UI.Services;

/// <summary>Coarse, payload-free diagnostics for the shared reconciliation adapter.</summary>
internal static partial class OptimisticReconcileLog
{
    /// <summary>Reports a retryable read failure using the caller's existing category.</summary>
    /// <param name="logger">The reconciliation service logger.</param>
    [LoggerMessage(0, LogLevel.Warning, "Reconcile re-read failed; will retry on the next confirm/poll.")]
    internal static partial void ReadFailed(ILogger logger);
}
