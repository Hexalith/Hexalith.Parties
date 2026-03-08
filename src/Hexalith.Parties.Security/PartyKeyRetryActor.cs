using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.Parties.Security;

public sealed partial class PartyKeyRetryActor(
    ActorHost host,
    PartyKeyManagementService keyManagementService,
    ILogger<PartyKeyRetryActor> logger) : Actor(host), IPartyKeyRetryActor, IRemindable
{
    private const string PendingStateKey = "crypto-pending";
    private const string RetryReminderName = "retry-key-creation";

    public async Task ScheduleRetryAsync(CryptoPendingRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        await StateManager.SetStateAsync(PendingStateKey, record).ConfigureAwait(false);
        await RegisterReminderAsync(
            RetryReminderName,
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1)).ConfigureAwait(false);

        logger.LogWarning(
            "Scheduled durable key retry for party {TenantId}/{PartyId}: {Reason}",
            record.TenantId,
            record.PartyId,
            record.LastError);
    }

    public async Task ClearRetryAsync()
    {
        _ = await StateManager.TryRemoveStateAsync(PendingStateKey).ConfigureAwait(false);
        await TryUnregisterReminderAsync().ConfigureAwait(false);
    }

    public async Task<bool> IsPendingAsync()
        => (await StateManager.TryGetStateAsync<CryptoPendingRecord>(PendingStateKey).ConfigureAwait(false)).HasValue;

    public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reminderName);

        if (!string.Equals(reminderName, RetryReminderName, StringComparison.Ordinal))
        {
            return;
        }

        ConditionalValue<CryptoPendingRecord> pending = await StateManager
            .TryGetStateAsync<CryptoPendingRecord>(PendingStateKey)
            .ConfigureAwait(false);

        if (!pending.HasValue)
        {
            await TryUnregisterReminderAsync().ConfigureAwait(false);
            return;
        }

        CryptoPendingRecord record = pending.Value;
        try
        {
            _ = await keyManagementService.CreateKeyAsync(record.TenantId, record.PartyId).ConfigureAwait(false);
            await ClearRetryAsync().ConfigureAwait(false);
            logger.LogInformation(
                "Durable key retry succeeded for party {TenantId}/{PartyId}.",
                record.TenantId,
                record.PartyId);
        }
        catch (Exception ex)
        {
            CryptoPendingRecord updated = record with
            {
                LastError = ex.Message,
                LastAttemptedAt = DateTimeOffset.UtcNow,
                AttemptCount = record.AttemptCount + 1,
            };

            await StateManager.SetStateAsync(PendingStateKey, updated).ConfigureAwait(false);
            logger.LogWarning(
                ex,
                "Durable key retry attempt {AttemptCount} failed for party {TenantId}/{PartyId}: {Error}",
                updated.AttemptCount,
                record.TenantId,
                record.PartyId,
                ex.Message);
        }
    }

    private async Task TryUnregisterReminderAsync()
    {
        try
        {
            await UnregisterReminderAsync(RetryReminderName).ConfigureAwait(false);
        }
        catch
        {
            // Best effort only.
        }
    }
}
