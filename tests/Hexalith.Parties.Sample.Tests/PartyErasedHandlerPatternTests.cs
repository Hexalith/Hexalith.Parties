using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Sample.Tests;

/// <summary>
/// Tests validating the PartyErased handler pattern documented in docs/event-handler-patterns.md.
/// These tests use a mock invoice store to demonstrate the mandatory handler implementation:
/// nullify party references, replace display names, preserve records, and log for audit.
/// </summary>
public sealed class PartyErasedHandlerPatternTests
{
    private const string ErasedPartyId = "p-erased-001";
    private static readonly string HandlerPatternsRelativePath = Path.Combine("docs", "event-handler-patterns.md");
    private static readonly string EventSubscribingRelativePath = Path.Combine("docs", "event-subscribing.md");

    private static readonly ConcurrentDictionary<string, MockInvoice> _invoiceStore = new();

    [Fact]
    public void Documentation_ShouldDescribeMandatoryPartyErasedHandlingAndBrokerLinks()
    {
        string documentation = File.ReadAllText(GetRepositoryFilePath(HandlerPatternsRelativePath));

        documentation.ShouldContain("PartyErased subscription is mandatory for ALL consuming applications");
        documentation.ShouldContain("../deploy/dapr/pubsub-kafka.yaml");
        documentation.ShouldContain("../deploy/dapr/pubsub-rabbitmq.yaml");
        documentation.ShouldContain("../deploy/dapr/pubsub-servicebus.yaml");
        documentation.ShouldContain("PartyErased handled: nullified party reference in {Count} invoices for {PartyId}");
    }

    [Fact]
    public void Documentation_ShouldDistinguishMvpSoftDeactivationFromFutureErasure()
    {
        string documentation = File.ReadAllText(GetRepositoryFilePath(HandlerPatternsRelativePath));
        string subscriberDocumentation = File.ReadAllText(GetRepositoryFilePath(EventSubscribingRelativePath));
        string combined = documentation + Environment.NewLine + subscriberDocumentation;

        combined.ShouldContain("MVP Soft Deactivation vs. Future Erasure");
        combined.ShouldContain("MVP delete operations are soft deactivations");
        combined.ShouldContain("PartyDeactivated");
        combined.ShouldContain("not legal erasure");
        combined.ShouldContain("MVP Compliance Boundary");
        combined.ShouldContain("manual deletion or environment rebuild");
        combined.ShouldContain("EventStore query gateway with `PartyDetail`");
        combined.ShouldContain("erasureStatus");
        combined.ShouldContain("verificationStatus");
        combined.ShouldContain("Pending or partial internal verification");
        combined.ShouldNotContain("GET /api/parties/{id}");
    }

    [Fact]
    public void Documentation_ShouldDescribeNormalizedEventDispatchForFullyQualifiedEventTypes()
    {
        string subscriberDocumentation = File.ReadAllText(GetRepositoryFilePath(EventSubscribingRelativePath));

        subscriberDocumentation.ShouldContain("Hexalith.Parties.Contracts.Events.PartyCreated");
        subscriberDocumentation.ShouldContain("NormalizeEventTypeName");
        subscriberDocumentation.ShouldContain("normalized as '{NormalizedEventType}'");
    }

    [Fact]
    public void Documentation_ShouldDescribeOrderingGuaranteesAndSequenceGuard()
    {
        string subscriberDocumentation = File.ReadAllText(GetRepositoryFilePath(EventSubscribingRelativePath));

        subscriberDocumentation.ShouldContain("Causal Ordering Guarantees Per Broker");
        subscriberDocumentation.ShouldContain("Aggregate-ID-based key routing");
        subscriberDocumentation.ShouldContain("Aggregate-ID as session key");
        subscriberDocumentation.ShouldContain("sequenceNumber");
        subscriberDocumentation.ShouldContain("Skipping out-of-order event");
        subscriberDocumentation.ShouldContain("Do not let an older update event recreate data that a newer cleanup event removed");
    }

    [Fact]
    public void Documentation_ShouldDescribeReadModelScopeAndPrivacy()
    {
        string documentation = File.ReadAllText(GetRepositoryFilePath(HandlerPatternsRelativePath));

        documentation.ShouldContain("Read Model Scope and Privacy");
        documentation.ShouldContain("store the stable `partyId`");
        documentation.ShouldContain("last processed aggregate `sequenceNumber`");
        documentation.ShouldContain("Add display names, contact values, identifiers, or natural-person flags only when your application actually needs them");
        documentation.ShouldContain("future erasure cleanup");
    }

    [Fact]
    public void HandlePartyErased_ShouldNullifyPartyReference()
    {
        // Arrange
        _invoiceStore.Clear();
        _invoiceStore["inv-1"] = new MockInvoice { Id = "inv-1", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 100m };
        _invoiceStore["inv-2"] = new MockInvoice { Id = "inv-2", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 250m };
        _invoiceStore["inv-3"] = new MockInvoice { Id = "inv-3", CustomerPartyId = "p-other", CustomerDisplayName = "Jean Martin", Amount = 50m };
        ILogger logger = Substitute.For<ILogger>();

        // Act
        HandlePartyErased(ErasedPartyId, logger);

        // Assert: party references nullified for affected invoices
        _invoiceStore["inv-1"].CustomerPartyId.ShouldBeNull();
        _invoiceStore["inv-2"].CustomerPartyId.ShouldBeNull();

        // Assert: unrelated invoice is untouched
        _invoiceStore["inv-3"].CustomerPartyId.ShouldBe("p-other");
    }

    [Fact]
    public void HandlePartyErased_ShouldReplaceDisplayNameWithErasedParty()
    {
        // Arrange
        _invoiceStore.Clear();
        _invoiceStore["inv-1"] = new MockInvoice { Id = "inv-1", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 100m };
        _invoiceStore["inv-2"] = new MockInvoice { Id = "inv-2", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 250m };
        _invoiceStore["inv-3"] = new MockInvoice { Id = "inv-3", CustomerPartyId = "p-other", CustomerDisplayName = "Jean Martin", Amount = 50m };
        ILogger logger = Substitute.For<ILogger>();

        // Act
        HandlePartyErased(ErasedPartyId, logger);

        // Assert: display names replaced for affected invoices
        _invoiceStore["inv-1"].CustomerDisplayName.ShouldBe("[Erased Party]");
        _invoiceStore["inv-2"].CustomerDisplayName.ShouldBe("[Erased Party]");

        // Assert: unrelated invoice display name untouched
        _invoiceStore["inv-3"].CustomerDisplayName.ShouldBe("Jean Martin");
    }

    [Fact]
    public void HandlePartyErased_ShouldPreserveRecordsWithIndependentRetention()
    {
        // Arrange
        _invoiceStore.Clear();
        _invoiceStore["inv-1"] = new MockInvoice { Id = "inv-1", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 100m };
        _invoiceStore["inv-2"] = new MockInvoice { Id = "inv-2", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 250m };
        _invoiceStore["inv-3"] = new MockInvoice { Id = "inv-3", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 75m };
        _invoiceStore["inv-4"] = new MockInvoice { Id = "inv-4", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 300m };
        ILogger logger = Substitute.For<ILogger>();

        // Act
        HandlePartyErased(ErasedPartyId, logger);

        // Assert: all 4 invoices still exist (independent legal retention: 7 years)
        _invoiceStore.Count.ShouldBe(4);
        _invoiceStore.ContainsKey("inv-1").ShouldBeTrue();
        _invoiceStore.ContainsKey("inv-2").ShouldBeTrue();
        _invoiceStore.ContainsKey("inv-3").ShouldBeTrue();
        _invoiceStore.ContainsKey("inv-4").ShouldBeTrue();

        // Assert: amounts are preserved (non-personal business data)
        _invoiceStore["inv-1"].Amount.ShouldBe(100m);
        _invoiceStore["inv-2"].Amount.ShouldBe(250m);
        _invoiceStore["inv-3"].Amount.ShouldBe(75m);
        _invoiceStore["inv-4"].Amount.ShouldBe(300m);
    }

    [Fact]
    public void HandlePartyErased_ShouldLogErasureForAuditTrail()
    {
        // Arrange
        _invoiceStore.Clear();
        _invoiceStore["inv-1"] = new MockInvoice { Id = "inv-1", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 100m };
        _invoiceStore["inv-2"] = new MockInvoice { Id = "inv-2", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 250m };
        ILogger logger = Substitute.For<ILogger>();

        // Act
        HandlePartyErased(ErasedPartyId, logger);

        // Assert: logger contains the documented audit trail details.
        object?[] logArguments = logger.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .GetArguments();

        logArguments[0].ShouldBe(LogLevel.Information);
        logArguments[2].ShouldNotBeNull();
        string renderedLogMessage = logArguments[2]?.ToString() ?? string.Empty;
        renderedLogMessage.ShouldContain("nullified party reference in 2 invoices");
        renderedLogMessage.ShouldContain(ErasedPartyId);
    }

    [Fact]
    public void HandlePartyErased_DuplicateDelivery_IsIdempotent()
    {
        _invoiceStore.Clear();
        _invoiceStore["inv-1"] = new MockInvoice { Id = "inv-1", CustomerPartyId = ErasedPartyId, CustomerDisplayName = "Marie Dupont", Amount = 100m };
        _invoiceStore["inv-2"] = new MockInvoice { Id = "inv-2", CustomerPartyId = "p-other", CustomerDisplayName = "Jean Martin", Amount = 50m };
        ILogger logger = Substitute.For<ILogger>();

        HandlePartyErased(ErasedPartyId, logger);
        HandlePartyErased(ErasedPartyId, logger);

        _invoiceStore["inv-1"].CustomerPartyId.ShouldBeNull();
        _invoiceStore["inv-1"].CustomerDisplayName.ShouldBe("[Erased Party]");
        _invoiceStore["inv-1"].Amount.ShouldBe(100m);
        _invoiceStore["inv-2"].CustomerPartyId.ShouldBe("p-other");
        _invoiceStore["inv-2"].CustomerDisplayName.ShouldBe("Jean Martin");
        logger.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .ShouldBe(2);
    }

    /// <summary>
    /// Implementation of the PartyErased handler pattern from docs/event-handler-patterns.md.
    /// This mirrors the documented Clara's journey example with an invoice store.
    /// </summary>
    private static void HandlePartyErased(string partyId, ILogger logger)
    {
        // Step 1: Find all local records referencing the erased partyId.
        List<MockInvoice> affectedInvoices = _invoiceStore.Values
            .Where(inv => inv.CustomerPartyId == partyId)
            .ToList();

        foreach (MockInvoice invoice in affectedInvoices)
        {
            // Step 2: Nullify the party reference.
            invoice.CustomerPartyId = null;

            // Step 3: Replace display names with "[Erased Party]".
            invoice.CustomerDisplayName = "[Erased Party]";

            // Step 4: Preserve the record (do NOT delete — independent legal retention).
        }

        // Step 5: Log the erasure handling for audit trail.
        logger.LogInformation(
            "PartyErased handled: nullified party reference in {Count} invoices for {PartyId}",
            affectedInvoices.Count,
            partyId);
    }

    private sealed class MockInvoice
    {
        public required string Id { get; init; }

        public string? CustomerPartyId { get; set; }

        public required string CustomerDisplayName { get; set; }

        public required decimal Amount { get; init; }
    }

    private static string GetRepositoryFilePath(string relativePath)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.Parties.slnx")))
            {
                return Path.Combine(current.FullName, relativePath);
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root for documentation validation.");
    }
}
