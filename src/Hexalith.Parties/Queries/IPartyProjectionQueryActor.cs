using Dapr.Actors;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Parties.Queries;

/// <summary>
/// Runtime-owned DAPR actor interface for the Parties projection query adapters.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IProjectionActor"/> is the implementation-neutral query-method contract and
/// intentionally does not depend on DAPR (so test fakes and adapter shims can implement it
/// without importing a DAPR package). DAPR actor hosts must therefore mirror that method on a
/// runtime-owned actor interface that ultimately derives from <see cref="IActor"/> — otherwise
/// <c>options.Actors.RegisterActor&lt;T&gt;()</c> throws because the actor type implements no
/// actor interface.
/// </para>
/// <para>
/// The query adapters keep implementing <see cref="IProjectionActor"/> for the neutral contract
/// and additionally implement this interface for the DAPR runtime, exactly as
/// <c>PartyIndexProjectionActor</c> pairs <c>IPartyIndexProjectionActor</c> with the non-actor
/// <c>IRemindable</c>. EventStore invokes the adapter through the weak actor-proxy path by the
/// registered actor type name and the <c>QueryAsync</c> method name, so this interface only needs
/// to surface the method to the actor runtime dispatcher.
/// </para>
/// </remarks>
public interface IPartyProjectionQueryActor : IActor
{
    /// <summary>
    /// Serves a projection query from a public query envelope.
    /// </summary>
    /// <param name="envelope">The query envelope carrying routing metadata and UTF-8 JSON payload bytes.</param>
    /// <returns>The query result containing payload bytes or an adapter-edge failure.</returns>
    Task<QueryResult> QueryAsync(QueryEnvelope envelope);
}
