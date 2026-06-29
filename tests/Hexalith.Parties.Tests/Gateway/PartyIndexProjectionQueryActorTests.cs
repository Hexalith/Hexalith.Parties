using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.Search;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Abstractions;
using Hexalith.Parties.Projections.Actors;
using Hexalith.Parties.Queries;
using Hexalith.Parties.Search;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.Parties.Tests.Gateway;

public sealed class PartyIndexProjectionQueryActorTests
{
    [Fact]
    public async Task QueryAsync_PartyIndex_ReadsTenantScopedIndexAndFiltersBeforePagingAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-alpha"] = Entry("p-alpha", "Alpha Person", PartyType.Person, active: true, "2026-05-03T00:00:00Z", "2026-05-05T00:00:00Z"),
                ["p-beta"] = Entry("p-beta", "Beta Person", PartyType.Person, active: true, "2026-05-04T00:00:00Z", "2026-05-06T00:00:00Z"),
                ["p-inactive"] = Entry("p-inactive", "Inactive Person", PartyType.Person, active: false, "2026-05-04T00:00:00Z", "2026-05-06T00:00:00Z"),
                ["p-org"] = Entry("p-org", "Org", PartyType.Organization, active: true, "2026-05-04T00:00:00Z", "2026-05-06T00:00:00Z"),
                ["p-outside"] = Entry("p-outside", "Outside Person", PartyType.Person, active: true, "2026-04-30T00:00:00Z", "2026-05-06T00:00:00Z"),
                ["p-erased"] = Entry("p-erased", "Erased Person", PartyType.Person, active: true, "2026-05-04T00:00:00Z", "2026-05-06T00:00:00Z") with { IsErased = true },
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new
            {
                page = 1,
                pageSize = 1,
                type = "Person",
                active = true,
                createdAfter = "2026-05-01T00:00:00.0000000+00:00",
                createdBefore = "2026-05-31T00:00:00.0000000+00:00",
                modifiedAfter = "2026-05-01T00:00:00.0000000+00:00",
                modifiedBefore = "2026-05-31T00:00:00.0000000+00:00",
            })));

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe(PartyIndexProjectionQueryActor.ProjectionType);
        PagedResult<PartyIndexEntry> page = DeserializePage(result);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(1);
        page.TotalCount.ShouldBe(2);
        page.TotalPages.ShouldBe(2);
        page.Items.Select(static i => i.Id).ShouldBe(["p-alpha"]);

        actorProxyFactory.Received(1).CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id != null && id.GetId() == "tenant-a:party-index"),
            nameof(PartyIndexProjectionActor),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_CurrentIndexProjection_EmitsCurrentFreshnessWithoutWarningsAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesReadAsync().Returns(Task.FromResult(new PartyIndexProjectionReadResult
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-current"] = Entry("p-current", "Current Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            },
            Freshness = new ProjectionFreshnessMetadata { Status = ProjectionFreshnessStatus.Current },
        }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        PagedResult<PartyIndexEntry> page = DeserializePage(result);
        page.Freshness.ShouldNotBeNull();
        page.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Current);
        page.Freshness.WarningCodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task QueryAsync_StaleIndexProjection_EmitsBoundedFreshnessAfterFilteringAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesReadAsync().Returns(Task.FromResult(new PartyIndexProjectionReadResult
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-visible"] = Entry("p-visible", "Visible Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
                ["p-erased"] = Entry("p-erased", "Erased Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z") with { IsErased = true },
            },
            Freshness = new ProjectionFreshnessMetadata
            {
                Status = ProjectionFreshnessStatus.Stale,
                WarningCodes = ["projection-state-store-unavailable"],
            },
        }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        PagedResult<PartyIndexEntry> page = DeserializePage(result);
        page.Items.Select(static i => i.Id).ShouldBe(["p-visible"]);
        page.TotalCount.ShouldBe(1);
        page.Freshness.ShouldNotBeNull();
        page.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Stale);
        page.Freshness.WarningCodes.ShouldBe(["projection-state-store-unavailable"]);
        result.GetPayload().GetRawText().ShouldNotContain("tenant-a:party-index", Case.Insensitive);
    }

    [Fact]
    public async Task QueryAsync_UnavailableIndexProjection_FailsClosedWithoutRowsAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesReadAsync().Returns(Task.FromResult(new PartyIndexProjectionReadResult
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-unsafe"] = Entry("p-unsafe", "Unsafe Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            },
            Freshness = new ProjectionFreshnessMetadata
            {
                Status = ProjectionFreshnessStatus.Unavailable,
                WarningCodes = ["projection-context-unavailable"],
            },
        }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorNotFoundInfrastructure);
        result.PayloadBytes.ShouldBeNull();
    }

    [Fact]
    public async Task QueryAsync_ActiveFalse_ReturnsInactiveEntriesWithoutHidingThemAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-active"] = Entry("p-active", "Active Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
                ["p-inactive"] = Entry("p-inactive", "Inactive Person", PartyType.Person, active: false, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 10, active = false })));

        result.Success.ShouldBeTrue();
        PagedResult<PartyIndexEntry> page = DeserializePage(result);
        page.Items.Select(static i => i.Id).ShouldBe(["p-inactive"]);
        page.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_PartySearch_ReadsTenantScopedIndexAndReturnsDisplayNameMetadataAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-exact"] = Entry("p-exact", "Acme", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
                ["p-prefix"] = Entry("p-prefix", "Acme Corporation", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
                ["p-other"] = Entry("p-other", "Other Tenant Name", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { query = "Acme", page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        result.ProjectionType.ShouldBe(PartyIndexProjectionQueryActor.ProjectionType);
        PagedResult<PartySearchResult> page = DeserializeSearchPage(result);
        page.Items.Select(static i => i.Party.Id).ShouldBe(["p-exact", "p-prefix"]);
        page.Items[0].Matches.ShouldContain(m => m.MatchedField == "displayName" && m.MatchType == "exact");
        page.Items[1].Matches.ShouldContain(m => m.MatchedField == "displayName" && m.MatchType == "prefix");
        page.Items.SelectMany(static i => i.Matches).ShouldAllBe(m => m.MatchedField == "displayName");

        actorProxyFactory.Received(1).CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id != null && id.GetId() == "tenant-a:party-index"),
            nameof(PartyIndexProjectionActor),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_PartySearch_CurrentLocalIndex_DoesNotInventDegradedMetadataAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesReadAsync().Returns(Task.FromResult(new PartyIndexProjectionReadResult
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-acme"] = Entry("p-acme", "Acme", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            },
            Freshness = new ProjectionFreshnessMetadata { Status = ProjectionFreshnessStatus.Current },
        }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { query = "Acme", page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        PagedResult<PartySearchResult> page = DeserializeSearchPage(result);
        page.Freshness.ShouldNotBeNull();
        page.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Current);
        page.Freshness.WarningCodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task QueryAsync_PartySearch_StaleIndexProjection_EmitsBoundedFreshnessAfterFilteringAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesReadAsync().Returns(Task.FromResult(new PartyIndexProjectionReadResult
        {
            Entries = new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-visible"] = Entry("p-visible", "Acme", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
                ["p-erased"] = Entry("p-erased", "Acme", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z") with { IsErased = true },
            },
            Freshness = new ProjectionFreshnessMetadata
            {
                Status = ProjectionFreshnessStatus.Stale,
                WarningCodes = ["projection-state-store-unavailable"],
            },
        }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { query = "Acme", page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        PagedResult<PartySearchResult> page = DeserializeSearchPage(result);
        page.Items.Select(static i => i.Party.Id).ShouldBe(["p-visible"]);
        page.TotalCount.ShouldBe(1);
        page.Freshness.ShouldNotBeNull();
        page.Freshness.Status.ShouldBe(ProjectionFreshnessStatus.Stale);
        page.Freshness.WarningCodes.ShouldBe(["projection-state-store-unavailable"]);
        string raw = result.GetPayload().GetRawText();
        raw.ShouldNotContain("tenant-a:party-index", Case.Insensitive);
        raw.ShouldNotContain("sequence", Case.Insensitive);
        raw.ShouldNotContain("stateKey", Case.Insensitive);
    }

    [Fact]
    public async Task QueryAsync_PartySearchPayloadWithUnknownFieldsRejectedBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-b:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-b",
            payload: Payload(new
            {
                query = "Shared Display Name",
                page = 1,
                pageSize = 20,
                tenantId = "tenant-a",
                actorId = "tenant-a:party-index",
            })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_InvalidDateRange_FailsBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new
            {
                page = 1,
                pageSize = 20,
                createdAfter = "2026-05-31T00:00:00.0000000+00:00",
                createdBefore = "2026-05-01T00:00:00.0000000+00:00",
            })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_InvalidPartyType_FailsBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 20, type = "999" })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    [Fact]
    public async Task QueryAsync_CrossTenantActorRoute_FailsBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-b",
            payload: Payload(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // P13: Stricter contract — unknown JSON fields fail closed instead of being silently dropped.
    // The security property (payload-supplied tenantId cannot influence routing) is now proven
    // even more strongly: the payload is rejected before route resolution runs, and no actor
    // proxy is constructed at all.
    [Fact]
    public async Task QueryAsync_PayloadWithUnknownFieldsRejectedBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-b:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-b",
            payload: Payload(new { page = 1, pageSize = 20, tenantId = "tenant-a", partitionKey = "tenant-a:party-index" })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // P13 companion: With only known fields in the payload, routing succeeds and the actor key
    // is derived strictly from the authenticated envelope tenant. This is the positive proof
    // that pairs with the unknown-fields negative proof above.
    [Fact]
    public async Task QueryAsync_KnownFieldsOnly_DerivesActorKeyFromEnvelopeTenantAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-tenant-b"] = Entry("p-tenant-b", "Tenant B Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-b:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-b",
            payload: Payload(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        DeserializePage(result).Items.Select(static i => i.Id).ShouldBe(["p-tenant-b"]);
        actorProxyFactory.Received(1).CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id != null && id.GetId() == "tenant-b:party-index"),
            nameof(PartyIndexProjectionActor),
            Arg.Any<ActorProxyOptions?>());
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id != null && id.GetId().Contains("tenant-a", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // P5: Malformed date string must short-circuit to InvalidEnvelope before any projection read.
    [Fact]
    public async Task QueryAsync_MalformedDateString_FailsBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 20, createdAfter = "not-a-date" })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // P6: Active filter null/omitted must return BOTH active and inactive entries (excluding erased).
    // Pins the implemented "no default active hiding" behavior so a future regression cannot
    // silently introduce active-only as the default without test failure.
    [Fact]
    public async Task QueryAsync_ActiveNull_ReturnsBothActiveAndInactiveEntriesAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-active"] = Entry("p-active", "Active Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
                ["p-inactive"] = Entry("p-inactive", "Inactive Person", PartyType.Person, active: false, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
                ["p-erased"] = Entry("p-erased", "Erased Person", PartyType.Person, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z") with { IsErased = true },
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 10 })));

        result.Success.ShouldBeTrue();
        PagedResult<PartyIndexEntry> page = DeserializePage(result);
        page.Items.Select(static i => i.Id).ShouldBe(["p-active", "p-inactive"], ignoreOrder: true);
        page.TotalCount.ShouldBe(2);
    }

    // P4: Erased entries on the PartySearch actor path must not surface in results or metadata.
    // The list path was already covered; this pins the search path equivalent against the actor
    // boundary (not just LocalPartySearchService).
    [Fact]
    public async Task QueryAsync_PartySearch_ErasedEntryInScope_ExcludedFromResultsAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-active"] = Entry("p-active", "Acme", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
                ["p-erased"] = Entry("p-erased", "Acme", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z") with { IsErased = true },
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { query = "Acme", page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        PagedResult<PartySearchResult> page = DeserializeSearchPage(result);
        page.Items.Select(static i => i.Party.Id).ShouldBe(["p-active"]);
        page.TotalCount.ShouldBe(1);
    }

    // P5: Cross-tenant search isolation at the actor boundary. The actor activated as tenant-b
    // must never return tenant-a entries even if display names collide. Pairs with
    // QueryAsync_KnownFieldsOnly_DerivesActorKeyFromEnvelopeTenantAsync which proves actor-key
    // derivation; this test proves results carry only the tenant-derived index actor's data.
    [Fact]
    public async Task QueryAsync_PartySearch_CrossTenantDisplayNameCollision_ReturnsOnlyOwnTenantEntriesAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor tenantBIndex = Substitute.For<IPartyIndexProjectionActor>();
        tenantBIndex.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-tenant-b-acme"] = Entry("p-tenant-b-acme", "Acme", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Is<ActorId>(id => id != null && id.GetId() == "tenant-b:party-index"),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(tenantBIndex);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-b:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-b",
            payload: Payload(new { query = "Acme", page = 1, pageSize = 20 })));

        result.Success.ShouldBeTrue();
        PagedResult<PartySearchResult> page = DeserializeSearchPage(result);
        page.Items.Select(static i => i.Party.Id).ShouldBe(["p-tenant-b-acme"]);
        page.TotalCount.ShouldBe(1);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Is<ActorId>(id => id != null && id.GetId().Contains("tenant-a", StringComparison.Ordinal)),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // P8: Envelope EntityId pointing at a sibling sub-resource must be rejected. Pins the
    // route-check arm against silent acceptance if EntityId resolves to anything other than
    // the parties list aggregate.
    [Fact]
    public async Task QueryAsync_EntityIdNotParties_FailsBeforeProjectionReadAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryEnvelope envelope = new(
            tenantId: "tenant-a",
            domain: PartyIndexProjectionQueryActor.PartyDomain,
            aggregateId: PartyIndexProjectionQueryActor.ListAggregateId,
            queryType: PartyIndexProjectionQueryActor.PartyIndexQueryType,
            payload: Payload(new { page = 1, pageSize = 20 }),
            correlationId: "corr-p8",
            userId: "user-1",
            entityId: "not-parties");

        QueryResult result = await actor.QueryAsync(envelope);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // Story 2.9: omitted mode and explicit Lexical/DisplayName remain the only MVP
    // display-name search modes accepted by the query actor.
    [Fact]
    public async Task QueryAsync_PartySearch_LexicalModeAndCaseId_AcceptedButHaveNoEffectOnDataSelectionAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)
            {
                ["p-acme"] = Entry("p-acme", "Acme", PartyType.Organization, active: true, "2026-05-01T00:00:00Z", "2026-05-01T00:00:00Z"),
            }));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult baseline = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { query = "Acme", page = 1, pageSize = 20 })));
        QueryResult withMode = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { query = "Acme", page = 1, pageSize = 20, mode = "Lexical", caseId = "case-X" })));
        QueryResult displayNameMode = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { query = "Acme", page = 1, pageSize = 20, mode = "DisplayName" })));

        baseline.Success.ShouldBeTrue();
        withMode.Success.ShouldBeTrue();
        displayNameMode.Success.ShouldBeTrue();
        IEnumerable<string> baselineIds = DeserializeSearchPage(baseline).Items.Select(static i => i.Party.Id);
        IEnumerable<string> withModeIds = DeserializeSearchPage(withMode).Items.Select(static i => i.Party.Id);
        withModeIds.ShouldBe(baselineIds);
        DeserializeSearchPage(displayNameMode).Items.Select(static i => i.Party.Id).ShouldBe(baselineIds);
    }

    [Theory]
    [InlineData("Hybrid")]
    [InlineData("Semantic")]
    [InlineData("Graph")]
    [InlineData("Email")]
    [InlineData("Identifier")]
    [InlineData("TemporalName")]
    public async Task QueryAsync_PartySearch_ReservedFutureModesReturnUnsupportedBeforeProjectionReadAsync(string mode)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateSearchEnvelope(
            tenant: "tenant-a",
            payload: Payload(new
            {
                query = "Sensitive Person",
                page = 1,
                pageSize = 20,
                mode,
                caseId = "case-secret",
            })));

        result.Success.ShouldBeFalse();
        result.PayloadBytes.ShouldBeNull();
        string errorMessage = result.ErrorMessage.ShouldNotBeNull();
        errorMessage.ShouldBe(QueryAdapterFailureReason.UnsupportedQueryType);
        errorMessage.ShouldNotContain("Sensitive", Case.Insensitive);
        errorMessage.ShouldNotContain("tenant-a", Case.Insensitive);
        errorMessage.ShouldNotContain("case-secret", Case.Insensitive);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // P17: Naive ISO-8601 timestamps (no Z, no +HH:MM offset) must be rejected as InvalidEnvelope.
    // Decision D5: explicit offset is required to prevent silent UTC assumption that skews the
    // filter by the caller's local offset.
    [Theory]
    [InlineData("2026-05-10T08:00:00")]
    [InlineData("2026-05-10T08:00:00.123")]
    [InlineData("2026-05-10")]
    public async Task QueryAsync_NaiveTimestampWithoutOffset_FailsBeforeProjectionReadAsync(string naiveTimestamp)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 20, createdAfter = naiveTimestamp })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.InvalidEnvelope);
        actorProxyFactory.DidNotReceive().CreateActorProxy<IPartyIndexProjectionActor>(
            Arg.Any<ActorId>(),
            Arg.Any<string>(),
            Arg.Any<ActorProxyOptions?>());
    }

    // P17 companion: timestamps with an explicit offset (Z, +HH:MM, -HH:MM) are accepted.
    [Theory]
    [InlineData("2026-05-10T08:00:00Z")]
    [InlineData("2026-05-10T08:00:00+02:00")]
    [InlineData("2026-05-10T08:00:00-05:30")]
    [InlineData("2026-05-10T08:00:00.123Z")]
    public async Task QueryAsync_TimestampWithExplicitOffset_AcceptedAsync(string explicitTimestamp)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Returns(Task.FromResult<IReadOnlyDictionary<string, PartyIndexEntry>>(
            new Dictionary<string, PartyIndexEntry>(StringComparer.Ordinal)));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        QueryResult result = await actor.QueryAsync(CreateEnvelope(
            tenant: "tenant-a",
            payload: Payload(new { page = 1, pageSize = 20, createdAfter = explicitTimestamp })));

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryAsync_OperationCanceledExceptionFromProjectionRead_PropagatesCancellationAsync()
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Throws(new OperationCanceledException());
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);
        PartyIndexProjectionQueryActor actor = CreateActor("party-index:tenant-a:parties", actorProxyFactory);

        await Should.ThrowAsync<OperationCanceledException>(
            () => actor.QueryAsync(CreateEnvelope("tenant-a", Payload(new { page = 1, pageSize = 20 }))));
    }

    // P8 + P14: Log-safety assertion strengthened.
    // P8: The exception message intentionally carries PII text ("Ada Lovelace") so the assertion
    //     actually catches a regression where the logger interpolates ex.Message instead of
    //     ex.GetType().Name. Without this, the negative-assertion was vacuously satisfied.
    // P14: The actor key "tenant-a:party-index" is now explicitly forbidden in log output
    //     to lock in the Party-Mode clarification that actor/storage keys are out of bounds.
    [Fact]
    public async Task QueryAsync_LogMessages_ContainOnlyBoundedMetadataOnReadFailureAsync()
    {
        var recordingLogger = new RecordingLogger<PartyIndexProjectionQueryActor>();
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IPartyIndexProjectionActor indexActor = Substitute.For<IPartyIndexProjectionActor>();
        indexActor.GetEntriesAsync().Throws(new InvalidOperationException(
            "state store failed while fetching entry for Ada Lovelace <ada@example.test>"));
        actorProxyFactory.CreateActorProxy<IPartyIndexProjectionActor>(
                Arg.Any<ActorId>(),
                Arg.Any<string>(),
                Arg.Any<ActorProxyOptions?>())
            .Returns(indexActor);

        PartyIndexProjectionQueryActor actor = CreateActorWithLogger(
            "party-index:tenant-a:parties",
            actorProxyFactory,
            recordingLogger);

        QueryResult result = await actor.QueryAsync(CreateEnvelope("tenant-a", Payload(new { page = 1, pageSize = 20 })));

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe(QueryAdapterFailureReason.ActorException);
        recordingLogger.Records.ShouldNotBeEmpty();
        // P11: Positive assertions. A regression that silently disables structured logging
        // would otherwise satisfy the ShouldNotContain assertions vacuously.
        recordingLogger.Records.ShouldContain(r => r.Message.Contains("PartyIndexQueryRouting", StringComparison.Ordinal));
        recordingLogger.Records.ShouldContain(r => r.Message.Contains("PartyIndexProjectionReadFailed", StringComparison.Ordinal));
        recordingLogger.Records.ShouldContain(r => r.Message.Contains("ExceptionType=InvalidOperationException", StringComparison.Ordinal));
        foreach (string message in recordingLogger.Records.Select(static r => r.Message))
        {
            // PII guards: nothing from the (intentionally PII-laden) exception message must leak.
            message.ShouldNotContain("Ada", Case.Insensitive);
            message.ShouldNotContain("ada@example.test", Case.Insensitive);
            message.ShouldNotContain("contactChannels", Case.Insensitive);
            // Actor/storage key guards (P14): neither the partitioned legacy form nor the
            // current tenant-scoped form is permitted in diagnostic output.
            message.ShouldNotContain("tenant-a:party-index:all", Case.Insensitive);
            message.ShouldNotContain("tenant-a:party-index", Case.Insensitive);
        }
    }

    private static PartyIndexProjectionQueryActor CreateActor(string actorId, IActorProxyFactory actorProxyFactory)
        => CreateActorWithLogger(actorId, actorProxyFactory, NullLogger<PartyIndexProjectionQueryActor>.Instance);

    private static PartyIndexProjectionQueryActor CreateActorWithLogger(
        string actorId,
        IActorProxyFactory actorProxyFactory,
        ILogger<PartyIndexProjectionQueryActor> logger)
    {
        ActorTimerManager timerManager = Substitute.For<ActorTimerManager>();
        var host = ActorHost.CreateForTest<PartyIndexProjectionQueryActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId), TimerManager = timerManager });
        IPartySearchProvider searchProvider = new LocalFuzzyPartySearchProvider();
        IHostApplicationLifetime hostLifetime = new StubHostApplicationLifetime();
        return new PartyIndexProjectionQueryActor(host, actorProxyFactory, searchProvider, hostLifetime, logger);
    }

    private sealed class StubHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }

    private static QueryEnvelope CreateEnvelope(string tenant, byte[] payload)
        => new(
            tenantId: tenant,
            domain: PartyIndexProjectionQueryActor.PartyDomain,
            aggregateId: PartyIndexProjectionQueryActor.ListAggregateId,
            queryType: PartyIndexProjectionQueryActor.PartyIndexQueryType,
            payload: payload,
            correlationId: "corr-list",
            userId: "user-1",
            entityId: PartyIndexProjectionQueryActor.ListAggregateId);

    private static QueryEnvelope CreateSearchEnvelope(string tenant, byte[] payload)
        => new(
            tenantId: tenant,
            domain: PartyIndexProjectionQueryActor.PartyDomain,
            aggregateId: PartyIndexProjectionQueryActor.ListAggregateId,
            queryType: "PartySearch",
            payload: payload,
            correlationId: "corr-search",
            userId: "user-1",
            entityId: PartyIndexProjectionQueryActor.ListAggregateId);

    private static PagedResult<PartyIndexEntry> DeserializePage(QueryResult result)
        => result.GetPayload().Deserialize<PagedResult<PartyIndexEntry>>(JsonOptions)
            ?? throw new InvalidOperationException("Expected paged PartyIndex payload.");

    private static PagedResult<PartySearchResult> DeserializeSearchPage(QueryResult result)
        => result.GetPayload().Deserialize<PagedResult<PartySearchResult>>(JsonOptions)
            ?? throw new InvalidOperationException("Expected paged PartySearch payload.");

    private static byte[] Payload<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);

    private static PartyIndexEntry Entry(
        string id,
        string displayName,
        PartyType type,
        bool active,
        string createdAt,
        string modifiedAt)
        => new()
        {
            Id = id,
            Type = type,
            IsActive = active,
            DisplayName = displayName,
            SortName = displayName,
            CreatedAt = DateTimeOffset.Parse(createdAt, System.Globalization.CultureInfo.InvariantCulture),
            LastModifiedAt = DateTimeOffset.Parse(modifiedAt, System.Globalization.CultureInfo.InvariantCulture),
        };

    // Mirror the real query client: results now serialize enums as strings via the canonical
    // wire options, so the test must read them back with the same converter set.
    private static readonly JsonSerializerOptions JsonOptions = PartiesJsonOptions.Default;

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Records.Add((logLevel, formatter(state, exception)));
        }
    }
}
