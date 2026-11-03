using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Introduces an actual competing SDK write after preparation, then forwards every operation to the real provider.</summary>
/// <param name="inner">The actual SDK provider store.</param>
/// <param name="beforeStage">The test-owned competing client action.</param>
internal sealed class PartyLegacyBackfillCompetingWriterStore(
    IReadModelBatchStagingStore inner,
    Func<ReadModelBatch, CancellationToken, Task> beforeStage) : IReadModelBatchStagingStore
{
    /// <inheritdoc/>
    public async Task<ReadModelBatchStagingResult> StageAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
    {
        await beforeStage(batch, cancellationToken).ConfigureAwait(false);
        return await inner.StageAsync(batch, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<ReadModelBatchStagingResult> CommitAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => inner.CommitAsync(batch, cancellationToken);

    /// <inheritdoc/>
    public Task<ReadModelBatchStagingResult> AbortAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => inner.AbortAsync(batch, cancellationToken);

    /// <inheritdoc/>
    public Task<ReadModelBatchStagingResult> VerifyAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        => inner.VerifyAsync(batch, cancellationToken);
}
