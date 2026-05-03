using System.Reflection;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Contracts.State;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.FitnessTests;

/// <summary>
/// Fitness test guarding the PartyState rejection-Apply ordering invariant. The rehydrator
/// resolves event types via short-name suffix match in metadata declaration order. If a
/// reformatter, code-organizer, or alphabetizer reorders the Apply methods, rejection events
/// like <c>PartyProcessingRestricted</c> would mis-route into the success-event handler
/// <c>Apply(ProcessingRestricted)</c> and silently corrupt state.
/// </summary>
public class PartyStateApplyOrderingFitnessTests
{
    [Fact]
    public void RejectionApplyMethodsAreDeclaredBeforeSuccessApplies()
    {
        MethodInfo[] applyMethods = [.. typeof(PartyState)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Apply")
            .Where(m => m.GetParameters().Length == 1)];

        applyMethods.ShouldNotBeEmpty("PartyState must declare at least one Apply method.");

        // Each Apply takes a single parameter — its event type. Group them by whether the type
        // is an IRejectionEvent.
        bool seenSuccess = false;
        foreach (MethodInfo method in applyMethods)
        {
            Type eventType = method.GetParameters()[0].ParameterType;
            bool isRejection = typeof(IRejectionEvent).IsAssignableFrom(eventType);

            if (!isRejection)
            {
                seenSuccess = true;
                continue;
            }

            // If we've already encountered a non-rejection Apply, the ordering invariant is broken:
            // a rejection Apply appears AFTER a success Apply. The rehydrator's suffix-match resolver
            // will route some events incorrectly.
            seenSuccess.ShouldBeFalse(
                $"PartyState.Apply({eventType.Name}) is a rejection-event handler but appears AFTER a success-event handler in declaration order. "
                + "All rejection-event Apply overloads must be declared BEFORE the first success-event Apply so the rehydrator's "
                + "short-name suffix matcher resolves them first. See PartyState.cs ORDERING NOTE.");
        }
    }

    [Fact]
    public void RejectionEventApplyHandlersExistForEverySuffixCollision()
    {
        // Concrete suffix-collision pairs we know about — keep this test in lockstep with the
        // rejection-Apply set declared in PartyState. If a new rejection event is added whose
        // short-name suffix collides with a success event's short name, declare its Apply
        // handler in PartyState (BEFORE the success Apply) and add the pair here.
        IReadOnlyList<(string Rejection, string Suffix)> knownCollisions =
        [
            ("PartyProcessingRestricted", "ProcessingRestricted"),
            ("PartyNotFound", "NotFound"),
            ("PartyNotRestricted", "NotRestricted"),
        ];

        IReadOnlySet<string> applyParamTypeNames = typeof(PartyState)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "Apply" && m.GetParameters().Length == 1)
            .Select(m => m.GetParameters()[0].ParameterType.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach ((string rejection, string suffix) in knownCollisions)
        {
            applyParamTypeNames.Contains(rejection).ShouldBeTrue(
                $"PartyState must declare Apply({rejection}) — its short name ends with '{suffix}' which collides with a success-event handler. "
                + "Without an explicit rejection-Apply, the rehydrator's suffix matcher will mis-route the event.");
        }
    }
}
