using System.Net;

namespace Hexalith.Parties.Client.Tests;

/// <summary>Signals response buffering and waits until its caller cancels the read.</summary>
/// <param name="reading">The signal that response serialization has started.</param>
internal sealed class CancellationResponseContent(TaskCompletionSource reading) : HttpContent
{
    /// <inheritdoc/>
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);

    /// <inheritdoc/>
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        reading.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
