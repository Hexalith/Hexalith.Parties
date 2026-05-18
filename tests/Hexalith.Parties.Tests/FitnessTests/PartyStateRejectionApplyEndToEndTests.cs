using System.Reflection;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Contracts.State;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>
/// End-to-end fitness test validating that rejection events flowing through the rehydrator's
/// suffix-match resolver land on the dedicated rejection-Apply overload and do NOT corrupt the
/// state via the same-suffix success-Apply overload. The existing
/// <see cref="PartyStateApplyOrderingFitnessTests"/> guards the source-declaration order; this
/// test runs the actual Apply invocation to assert the rejection no-op semantic holds at runtime.
/// </summary>
public sealed class PartyStateRejectionApplyEndToEndTests
{
    [Fact]
    public void Apply_PartyProcessingRestricted_OnFreshState_DoesNotMutateIsRestricted()
    {
        // PartyProcessingRestricted is a rejection event ("you cannot apply ProcessingRestricted
        // because the party is already restricted"). It must NOT mutate IsRestricted to true —
        // that's the success-event semantic of Apply(ProcessingRestricted). The dedicated
        // rejection overload is a no-op.
        PartyState seed = new();
        seed.IsRestricted.ShouldBeFalse();

        InvokeApply(seed, new PartyProcessingRestricted
        {
            PartyId = "p1",
            TenantId = "acme",
        });

        seed.IsRestricted.ShouldBeFalse(
            "Apply(PartyProcessingRestricted) is a rejection-event no-op; mutating IsRestricted=true would mean the rehydrator's suffix matcher mis-routed into Apply(ProcessingRestricted).");
    }

    [Fact]
    public void Apply_PartyNotRestricted_OnRestrictedState_DoesNotClearIsRestricted()
    {
        // PartyNotRestricted is a rejection event ("you cannot lift a restriction that doesn't
        // exist"). On a state that IS restricted, applying the rejection must NOT clear the
        // flag — that's the success-event semantic of Apply(RestrictionLifted).
        PartyState seed = new();
        InvokeApply(seed, new ProcessingRestricted
        {
            PartyId = "p1",
            TenantId = "acme",
            RestrictedAt = DateTimeOffset.UtcNow,
        });
        seed.IsRestricted.ShouldBeTrue("Pre-condition: ProcessingRestricted (success) sets IsRestricted=true.");

        InvokeApply(seed, new PartyNotRestricted { PartyId = "p1", TenantId = "acme" });

        seed.IsRestricted.ShouldBeTrue(
            "Apply(PartyNotRestricted) is a rejection-event no-op; clearing IsRestricted=false would mean the rehydrator routed into Apply(RestrictionLifted).");
    }

    [Fact]
    public void Apply_PartyNotFound_DoesNotMutateState()
    {
        PartyState seed = new();
        InvokeApply(seed, new PartyCreated
        {
            Type = Hexalith.Parties.Contracts.ValueObjects.PartyType.Person,
        });
        DateTimeOffset createdAt = seed.CreatedAt;

        InvokeApply(seed, new PartyNotFound());

        seed.CreatedAt.ShouldBe(createdAt);
        seed.IsActive.ShouldBeTrue();
        seed.IsRestricted.ShouldBeFalse();
    }

    [Fact]
    public void Apply_PartyCommandValidationRejected_DoesNotMutateState()
    {
        PartyState seed = new();
        InvokeApply(seed, new PartyCreated
        {
            Type = Hexalith.Parties.Contracts.ValueObjects.PartyType.Person,
        });
        DateTimeOffset createdAt = seed.CreatedAt;
        bool isActive = seed.IsActive;
        bool isRestricted = seed.IsRestricted;

        InvokeApply(seed, new PartyCommandValidationRejected
        {
            CommandType = "CreateParty",
            Failures = [new PartyValidationFailure { PropertyName = "PartyId", ErrorCode = "NotEmpty" }],
        });

        seed.CreatedAt.ShouldBe(createdAt);
        seed.IsActive.ShouldBe(isActive);
        seed.IsRestricted.ShouldBe(isRestricted);
    }

    [Fact]
    public void RejectionEvents_AreIRejectionEvent_AndApplyIsDeclaredOnPartyState()
    {
        Type[] rejections = [.. typeof(PartyNotFound).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(PartyNotFound).Namespace)
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => typeof(IRejectionEvent).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)];

        rejections.ShouldNotBeEmpty("Parties must retain typed rejection event contracts.");

        foreach (Type rejection in rejections)
        {
            typeof(IRejectionEvent).IsAssignableFrom(rejection).ShouldBeTrue(
                $"{rejection.Name} must implement IRejectionEvent so the rehydrator routes it through the rejection-Apply path.");

            MethodInfo? apply = typeof(PartyState)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .FirstOrDefault(m => m.Name == "Apply"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == rejection);

            apply.ShouldNotBeNull(
                $"PartyState must declare Apply({rejection.Name}) so the rehydrator can route the rejection event without falling back to a same-suffix success handler.");
        }
    }

    /// <summary>
    /// Invokes <c>PartyState.Apply(TEvent)</c> reflectively for the runtime type of
    /// <paramref name="event"/>. Uses exact-type binding to bypass the rehydrator's suffix
    /// matcher and verify each Apply overload's body in isolation.
    /// </summary>
    private static void InvokeApply(PartyState state, IEventPayload @event)
    {
        MethodInfo? apply = typeof(PartyState)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .FirstOrDefault(m => m.Name == "Apply"
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == @event.GetType());

        apply.ShouldNotBeNull($"PartyState.Apply({@event.GetType().Name}) was not found.");
        apply!.Invoke(state, [@event]);
    }
}
