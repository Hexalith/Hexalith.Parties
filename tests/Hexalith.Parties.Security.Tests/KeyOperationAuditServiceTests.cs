#pragma warning disable CS8620 // NSubstitute + DaprClient nullable generics mismatch

using Dapr.Client;

using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public class KeyOperationAuditServiceTests
{
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();

    private KeyOperationAuditService CreateService() => new(_daprClient);

    [Fact]
    public async Task RecordOperationAsync_SavesEntryToStateStore()
    {
        var entry = new KeyOperationAuditEntry
        {
            OperationType = KeyOperationType.Create,
            TenantId = "acme",
            PartyId = "p1",
            KeyVersion = 1,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "corr-1",
        };

        // Configure ETag-based read/write
        _daprClient.GetStateAndETagAsync<List<KeyOperationAuditEntry>>(
            "statestore",
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns((null as List<KeyOperationAuditEntry>, ""));

        _daprClient.TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<List<KeyOperationAuditEntry>>(),
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateService().RecordOperationAsync(entry);

        await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            Arg.Is<string>(k => k != null && k.Contains("acme") && k.Contains("p1")),
            Arg.Any<List<KeyOperationAuditEntry>>(),
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuditTrailAsync_ReturnsEntriesFromStateStore()
    {
        var entries = new List<KeyOperationAuditEntry>
        {
            new()
            {
                OperationType = KeyOperationType.Create,
                TenantId = "acme",
                PartyId = "p1",
                KeyVersion = 1,
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = "corr-1",
            },
        };

        _daprClient.GetStateAsync<List<KeyOperationAuditEntry>>(
            "statestore",
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => entries);

        IReadOnlyList<KeyOperationAuditEntry> result =
            await CreateService().GetAuditTrailAsync("acme", "p1");

        result.Count.ShouldBe(1);
        result[0].OperationType.ShouldBe(KeyOperationType.Create);
    }

    [Fact]
    public async Task GetAuditTrailAsync_ReturnsEmpty_WhenNoEntries()
    {
        _daprClient.GetStateAsync<List<KeyOperationAuditEntry>>(
            "statestore",
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<KeyOperationAuditEntry>?)null);

        IReadOnlyList<KeyOperationAuditEntry> result =
            await CreateService().GetAuditTrailAsync("acme", "p1");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordOperationAsync_AppendsToExistingEntries()
    {
        var existing = new List<KeyOperationAuditEntry>
        {
            new()
            {
                OperationType = KeyOperationType.Create,
                TenantId = "acme",
                PartyId = "p1",
                KeyVersion = 1,
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
                CorrelationId = "corr-1",
            },
        };

        _daprClient.GetStateAndETagAsync<List<KeyOperationAuditEntry>>(
            "statestore",
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns((existing, "etag-1"));

        _daprClient.TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<List<KeyOperationAuditEntry>>(),
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        var newEntry = new KeyOperationAuditEntry
        {
            OperationType = KeyOperationType.Rotate,
            TenantId = "acme",
            PartyId = "p1",
            KeyVersion = 2,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "corr-2",
        };

        await CreateService().RecordOperationAsync(newEntry);

        await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Is<List<KeyOperationAuditEntry>>(list => list != null && list.Count == 2),
            "etag-1",
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordOperationAsync_DoesNotMutateFetchedStateInstance()
    {
        var existing = new List<KeyOperationAuditEntry>
        {
            new()
            {
                OperationType = KeyOperationType.Create,
                TenantId = "acme",
                PartyId = "p1",
                KeyVersion = 1,
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1),
                CorrelationId = "corr-1",
            },
        };

        _daprClient.GetStateAndETagAsync<List<KeyOperationAuditEntry>>(
            "statestore",
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns((existing, "etag-1"));

        _daprClient.TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<List<KeyOperationAuditEntry>>(),
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateService().RecordOperationAsync(new KeyOperationAuditEntry
        {
            OperationType = KeyOperationType.Read,
            TenantId = "acme",
            PartyId = "p1",
            KeyVersion = 1,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "corr-2",
        });

        existing.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RecordOperationAsync_RetriesOnETagConflict()
    {
        _daprClient.GetStateAndETagAsync<List<KeyOperationAuditEntry>>(
            "statestore",
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(
                (null as List<KeyOperationAuditEntry>, "etag-1"),
                (null as List<KeyOperationAuditEntry>, "etag-2"));

        // First attempt fails (ETag mismatch), second succeeds
        _daprClient.TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<List<KeyOperationAuditEntry>>(),
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(false, true);

        var entry = new KeyOperationAuditEntry
        {
            OperationType = KeyOperationType.Create,
            TenantId = "acme",
            PartyId = "p1",
            KeyVersion = 1,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = "corr-1",
        };

        await CreateService().RecordOperationAsync(entry);

        // Should have been called twice (retry)
        await _daprClient.Received(2).TrySaveStateAsync(
            "statestore",
            Arg.Any<string>(),
            Arg.Any<List<KeyOperationAuditEntry>>(),
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }
}
