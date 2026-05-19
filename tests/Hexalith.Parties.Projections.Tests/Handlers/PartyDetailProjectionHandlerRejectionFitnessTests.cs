using System.Reflection;
using System.Runtime.CompilerServices;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Projections.Handlers;
using Hexalith.Parties.Testing;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Handlers;

/// <summary>
/// Reflection-based fitness check: every <see cref="IRejectionEvent"/> declared in the Parties
/// contracts assembly must round-trip through <see cref="PartyDetailProjectionHandler.Apply"/>
/// as a no-mutation signal (returns <c>null</c>) for both null and populated state. This guards
/// against a future rejection type being absorbed by the default switch arm without coverage.
/// </summary>
public sealed class PartyDetailProjectionHandlerRejectionFitnessTests
{
    private const string PartyId = PartyTestData.DefaultPartyId;

    public static TheoryData<Type> RejectionEventTypes()
    {
        TheoryData<Type> data = [];
        foreach (Type type in typeof(PartyCreated).Assembly.GetTypes())
        {
            if (type.IsClass && !type.IsAbstract && typeof(IRejectionEvent).IsAssignableFrom(type))
            {
                data.Add(type);
            }
        }
        return data;
    }

    [Fact]
    public void RejectionEventTypes_AtLeastOneTypeDiscovered()
    {
        // Sanity check: if reflection finds zero rejection types we'd silently pass the theory.
        RejectionEventTypes().Count.ShouldBeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(RejectionEventTypes))]
    public void Apply_AnyRejectionEvent_WithNullState_ReturnsNull(Type rejectionType)
    {
        ArgumentNullException.ThrowIfNull(rejectionType);
        IEventPayload rejection = CreateRejectionInstance(rejectionType);

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, rejection, null);

        result.ShouldBeNull($"{rejectionType.Name} against null state must be a no-op");
    }

    [Theory]
    [MemberData(nameof(RejectionEventTypes))]
    public void Apply_AnyRejectionEvent_WithPopulatedState_ReturnsNull(Type rejectionType)
    {
        ArgumentNullException.ThrowIfNull(rejectionType);
        IEventPayload rejection = CreateRejectionInstance(rejectionType);
        PartyDetail state = CreatePopulatedState();

        PartyDetail? result = PartyDetailProjectionHandler.Apply(PartyId, rejection, state);

        result.ShouldBeNull($"{rejectionType.Name} against populated state must be a no-op");
    }

    private static IEventPayload CreateRejectionInstance(Type rejectionType)
    {
        // Rejection records may carry `required` members (e.g. PartyCommandValidationRejected).
        // The handler never reads payload fields for rejections, so an uninitialised instance is
        // safe and avoids coupling this fitness test to record schema changes.
        object instance = RuntimeHelpers.GetUninitializedObject(rejectionType);
        return instance is IEventPayload payload
            ? payload
            : throw new InvalidOperationException($"{rejectionType.FullName} could not be cast to IEventPayload.");
    }

    private static PartyDetail CreatePopulatedState() =>
        new()
        {
            Id = PartyId,
            Type = PartyType.Person,
            IsActive = true,
            DisplayName = "John Doe",
            SortName = "Doe, John",
            PersonDetails = PartyTestData.ValidPersonDetails(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
}
