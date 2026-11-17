using System.Text.Json;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Retains authenticated startup ownership as the sole authority for emergency reconciliation.</summary>
internal sealed class PartyMemoryRunnerInterruptionCustody
{
    private PartyMemoryRunnerInterruptionCustody(JsonElement topology) => Topology = topology.Clone();

    /// <summary>Gets immutable startup ownership; rejected phase evidence cannot replace these targets.</summary>
    internal JsonElement Topology { get; private set; }

    /// <summary>Gets the original disposable session bound to the authenticated runner incarnation.</summary>
    internal string Session => Topology.GetProperty("Id").GetString()!;

    /// <summary>Issues external custody before Process.Start, independent of any successful runner publication.</summary>
    internal static PartyMemoryRunnerInterruptionCustody Authorize(string session, string root, string marker, string lineage)
        => new(PartyMemoryRunnerInterruptionAudit.Authorize(session, root, marker, lineage));

    /// <summary>Retains incremental immutable resource identities using only externally issued labels and lineage.</summary>
    internal async Task RefreshAsync(CancellationToken token = default, Func<CancellationToken, string[], Task<string>>? command = null)
        => Topology = await PartyMemoryRunnerInterruptionAudit.DiscoverAsync(Topology, captured => Topology = captured, token, command).ConfigureAwait(false);

    /// <summary>Admits only a startup-role receipt matching the independently launched runner and private nonce.</summary>
    internal static PartyMemoryRunnerInterruptionCustody Authenticate(JsonElement message, string phase, string secret, string nonce, int pid, long start)
    {
        if (message.GetProperty("Secret").GetString() != secret || message.GetProperty("Nonce").GetString() != nonce
            || message.GetProperty("Phase").GetString() != phase || message.GetProperty("RunnerPid").GetInt32() != pid
            || message.GetProperty("RunnerStart").GetInt64() != start
            || message.GetProperty("Observation").GetProperty("Phase").GetString() != "startup-custody-only")
        { throw new InvalidDataException("Startup custody does not match this owned runner and receipt role."); }
        return new(message.GetProperty("Topology"));
    }

    /// <summary>Invokes recovery exclusively with the authenticated startup targets, including their late-worker ownership marker.</summary>
    internal Task RecoverAsync(Func<JsonElement, Task> recovery) => recovery(Topology);

    /// <summary>Retains partial recovery discovery even when a later independent command fails.</summary>
    internal async Task RecoverAsync()
        => await PartyMemoryRunnerInterruptionAudit.RecoverAsync(Topology, captured => Topology = captured).ConfigureAwait(false);
}
