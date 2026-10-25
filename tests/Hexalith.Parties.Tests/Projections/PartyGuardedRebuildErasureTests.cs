#if HEXALITH_EVENTSTORE_NONEXPIRING_RECOVERY
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Configuration;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Projections.Models;
using Hexalith.Parties.Projections.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.Parties.Tests.Projections;

/// <summary>Qualifies guarded SDK private preparation against the actual Parties plan and fresh erasure/dispatcher instances.</summary>
public sealed class PartyGuardedRebuildErasureTests
{
    private const string Store = "statestore";
    private const string Secret = "PrivateSentinel";
    private const string Operation = "guarded-rebuild";
    private static readonly string s_detail = PartySdkReadModelAddresses.Detail("tenant-a", "party-1");
    // This fixture creates and owns every canonical, private and foreign value in this synthetic store.
    private static readonly IOptions<PartySdkReadModelOptions> s_options = Options.Create(new PartySdkReadModelOptions
    { ReadModelStateStoreName = Store, HistoricalPrivateRecoveryReconciliationAttested = true });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedPublicDispatcherPreparationIsDiscoverableAndFreshTerminalVerificationCannotBypassErasure(bool crashBeforeParticipant)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new InMemoryReadModelStore();
        var events = Enumerable.Range(1, 40).Select(sequence =>
        {
            object payload = sequence == 1
                ? new PartyCreated { Type = PartyType.Person, PersonDetails = new PersonDetails { FirstName = Secret, LastName = "Owner" } }
                : new PartyDeactivated();
            return new ProjectionEventDto(payload.GetType().Name, JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), PartiesJsonOptions.Default),
                "json", sequence, DateTimeOffset.UnixEpoch.AddSeconds(sequence), "correlation", sequence == 40 ? Operation : "event-" + sequence);
        }).ToArray();
        var history = new ProjectionRequest("tenant-a", "party", "party-1", events) { RebuildSourceHeadSequence = 40 };
        SetContext("RebuildSourceHeadMessageId", Operation);
        SetContext("RebuildOwnerGenerations", new Dictionary<string, string> { [PartyProjectionNames.Detail] = "generation-1" });
        SetContext("RebuildOwnerIdentities", new Dictionary<string, string> { [PartyProjectionNames.Detail] = "owner-1" });
        var identity = new DomainProjectionIdentityOptions { AppId = "parties-private-recovery", ServiceVersion = "v1" };
        var capability = new ProjectionSourceGuardCapability(1, 1, 1, "full-replay-v1", "projection-owner-proof/v1", null, null);
        var route = new ProjectionDispatchRoute("party", PartyProjectionNames.Detail) { SourceGuard = capability };
        string catalog = ProjectionRouteCatalogFingerprint.Compute(identity.AppId, identity.ServiceVersion,
            [route], [new("party", PartyProjectionNames.Detail)]);
        var request = new ProjectionDispatchRequest(history, [PartyProjectionNames.Detail], Operation, catalog);
        var dispatchOptions = new ProjectionDispatchOptions();
        (await new PartyDetailSdkProjectionHandler(store, store, s_options).ProjectAsync(history, "seed", token)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.SeedRaw(Store, "foreign-survivor", new { Value = "unchanged" });
        byte[] foreign = store.SnapshotRaw(Store, "foreign-survivor")!;
        if (crashBeforeParticipant)
            store.BatchFaultHook = (phase, _, _) => phase == ReadModelBatchPhase.ManifestPrepared
                ? throw new IOException("crash after registered private manifest") : Task.CompletedTask;
        using (var first = Provider())
        {
            try
            {
                var staged = await DomainProjectionDispatcher.StageRebuildAsync(first, request, dispatchOptions, identity, token);
                staged.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(crashBeforeParticipant ? ProjectionDispatchStatus.Indeterminate : ProjectionDispatchStatus.Completed);
            }
            catch (ProjectionDispatchValidationException) when (crashBeforeParticipant) { }
        }
        store.SnapshotRawEntries(Store).Where(pair => pair.Key != s_detail).ShouldContain(pair => ContainsSecret(pair.Value));
        if (crashBeforeParticipant) store.HasPendingEnvelope(Store, s_detail).ShouldBeFalse();
        store.BatchFaultHook = null;
        if (!crashBeforeParticipant)
        {
            using (var freshCommit = Provider())
                (await DomainProjectionDispatcher.CommitRebuildAsync(freshCommit, request, dispatchOptions, identity, token))
                    .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
            // New service instances verify current ownership through content-free SDK completion evidence.
            using (var freshVerify = Provider())
            {
                (await DomainProjectionDispatcher.VerifyRebuildAsync(freshVerify, request, dispatchOptions, identity, token))
                    .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
                (await DomainProjectionDispatcher.CommitRebuildAsync(freshVerify, request, dispatchOptions, identity, token))
                    .Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
            }
            store.SnapshotRawEntries(Store).Where(pair => pair.Key != s_detail).ShouldNotContain(pair => ContainsSecret(pair.Value));
        }
        await new PartySdkReadModelEraser(store, store, s_options).EraseAsync("tenant-a", "party-1", token);
        store.SnapshotRawEntries(Store).ShouldNotContain(pair => ContainsSecret(pair.Value));
        store.SnapshotRaw(Store, "foreign-survivor").ShouldBe(foreign);
        (await store.GetAsync<PartyDetailSdkReadModel>(Store, s_detail, token)).Value!.Detail!.PersonDetails.ShouldBeNull();
        using var stale = Provider();
        await RefusesAsync(() => DomainProjectionDispatcher.StageRebuildAsync(stale, request, dispatchOptions, identity, token));
        await RefusesAsync(() => DomainProjectionDispatcher.VerifyRebuildAsync(stale, request, dispatchOptions, identity, token));
        await RefusesAsync(() => DomainProjectionDispatcher.CommitRebuildAsync(stale, request, dispatchOptions, identity, token));
        store.SnapshotRawEntries(Store).ShouldNotContain(pair => ContainsSecret(pair.Value));
        store.SnapshotRaw(Store, "foreign-survivor").ShouldBe(foreign);

        void SetContext(string property, object value)
            => typeof(ProjectionRequest).GetProperty(property, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(history, value);

        ServiceProvider Provider()
        {
            var actual = new PartyDetailSdkProjectionHandler(store, store, s_options);
            var wrapped = Substitute.For<IAsyncDomainProjectionRebuildHandler>();
            wrapped.Domain.Returns("party");
            wrapped.ProjectionType.Returns(PartyProjectionNames.Detail);
            wrapped.OrdinaryDeliveryEnabled.Returns(true);
            wrapped.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
            wrapped.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                var plan = await actual.PrepareRebuildAsync(call.Arg<ProjectionRequest>(), call.Arg<string>(), call.Arg<CancellationToken>());
                plan.DiscoverableRecovery.ShouldBeTrue();
                var owner = new ProjectionOwnerSelection(1, "tenant-a", "party", "party-1", PartyProjectionNames.Detail,
                    "owner-1", "generation-1", 40, Operation, Operation, "40") { ReceiptProjectionName = ReadModelRebuildCompletion.ReceiptProjectionName };
                var operations = plan.Operations.Select(operation =>
                {
                    var value = JsonNode.Parse(operation.CanonicalValue.Span)!.AsObject();
                    value["projectionOwner"] = JsonSerializer.SerializeToNode(owner, JsonSerializerOptions.Web);
                    return ReadModelBatchOperation.Write(operation.Key, value, operation.Concurrency);
                }).ToArray();
                return new DomainProjectionRebuildPlan(Store, operations) { StagedOperations = plan.StagedOperations, DiscoverableRecovery = true };
            });
            return new ServiceCollection().AddSingleton<IReadModelStore>(store).AddSingleton<IReadModelBatchStore>(store)
                .AddSingleton<IReadModelBatchStagingStore>(store).AddSingleton<IReadModelOwnerProofStore>(store)
                .AddSingleton<IReadModelConditionalEraser>(store).AddSingleton<IAsyncDomainProjectionHandler>(wrapped)
                .AddSingleton(new DomainProjectionOwnerProofRegistration("party", PartyProjectionNames.Detail, capability,
                    new(Store, "detail") { SelectionKey = _ => s_detail })).BuildServiceProvider();
        }
    }

    private static async Task RefusesAsync(Func<Task<ProjectionDispatchResponse>> action)
    {
        try { (await action()).Outcomes.ShouldAllBe(outcome => outcome.Status is not (ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted)); }
        catch (ProjectionDispatchValidationException) { }
    }

    private static bool ContainsSecret(byte[] bytes)
    {
        string text = Encoding.UTF8.GetString(bytes);
        if (text.Contains(Secret, StringComparison.Ordinal)) return true;
        int json = text.IndexOf('{');
        if (json > 0) text = text[json..]; // accepted legacy private prefix, if one was incorrectly retained
        try
        {
            using var document = JsonDocument.Parse(text);
            return Visit(document.RootElement);
        }
        catch (JsonException) { return false; }

        static bool Visit(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object) return value.EnumerateObject().Any(property => Visit(property.Value));
            if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().Any(Visit);
            if (value.ValueKind != JsonValueKind.String) return false;
            try { return ContainsSecret(Convert.FromBase64String(value.GetString()!)); }
            catch (FormatException) { return false; }
        }
    }
}
#endif
