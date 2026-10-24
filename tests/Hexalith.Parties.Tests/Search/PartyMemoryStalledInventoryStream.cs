namespace Hexalith.Parties.Tests.Search;

/// <summary>A body that stalls until its actual asynchronous read token is cancelled.</summary>
internal sealed class PartyMemoryStalledInventoryStream : MemoryStream
{
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
