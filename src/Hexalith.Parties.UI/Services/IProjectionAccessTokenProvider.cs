namespace Hexalith.Parties.UI.Services;

/// <summary>
/// The pluggable access-token seam for the EventStore SignalR connection (Story 1.7, AR-D6). The adapter
/// feeds <see cref="GetAccessTokenAsync"/> to <c>EventStoreSignalRClientOptions.AccessTokenProvider</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>⚠️ Blazor-Server token trap (live capture deferred).</strong> In a live Interactive Server
/// circuit <c>HttpContext</c> is <see langword="null"/>, so an implementation must <strong>never</strong>
/// call <c>HttpContext.GetTokenAsync</c> from inside the connect / reconnect callback. The correct pattern
/// (FrontComposer / Tenants) is to capture the server-side OIDC access token at circuit start (from the
/// auth ticket / a server-side token store) and yield it here. That live per-circuit capture lands with
/// the first authenticated data screen (Epic 2/4); this story ships only the seam, defaulting to
/// "token if available, else <see langword="null"/>" (the hub may admit the server-side caller without a
/// per-user bearer in dev).
/// </para>
/// </remarks>
public interface IProjectionAccessTokenProvider
{
    /// <summary>
    /// Yields the circuit's server-side OIDC access token when one is available, or <see langword="null"/>
    /// otherwise. Must not touch <c>HttpContext</c> from a circuit callback.
    /// </summary>
    Task<string?> GetAccessTokenAsync();
}

/// <summary>
/// The default, inert access-token provider — always yields <see langword="null"/>. Replaced by the live
/// per-circuit capture when the first authenticated data screen lands (Epic 2/4).
/// </summary>
internal sealed class NullProjectionAccessTokenProvider : IProjectionAccessTokenProvider
{
    public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);
}
