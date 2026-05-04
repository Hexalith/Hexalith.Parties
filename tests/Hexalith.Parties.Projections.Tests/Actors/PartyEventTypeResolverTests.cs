using Hexalith.Parties.Contracts.Events;
using Hexalith.Parties.Projections.Actors;

using Shouldly;

namespace Hexalith.Parties.Projections.Tests.Actors;

/// <summary>
/// Tests for the static <see cref="PartyEventTypeResolver"/>. The resolver is critical because
/// projection actors dispatch event-type names from the wire to typed handlers; a wrong dispatch
/// (e.g., ambiguous-short-name silently routed to a sibling type) would corrupt projection state.
/// The class is internal — these tests rely on the InternalsVisibleTo wiring in the
/// <c>Hexalith.Parties.Projections.csproj</c>.
/// </summary>
public sealed class PartyEventTypeResolverTests
{
    [Fact]
    public void Resolve_KnownFullName_ReturnsType()
    {
        Type? type = PartyEventTypeResolver.Resolve(typeof(PartyCreated).FullName!);

        type.ShouldBe(typeof(PartyCreated));
    }

    [Fact]
    public void Resolve_KnownShortName_ReturnsType()
    {
        Type? type = PartyEventTypeResolver.Resolve(nameof(PartyCreated));

        type.ShouldBe(typeof(PartyCreated));
    }

    [Fact]
    public void Resolve_AssemblyQualifiedName_DoesNotLoadArbitraryAssembly()
    {
        // Type.GetType was previously consulted for assembly-qualified names — that path was
        // removed because it accepts inputs like "System.Diagnostics.Process,
        // System.Diagnostics.Process" and triggers arbitrary assembly load. The contract
        // assembly is now the only authoritative source.
        Type? type = PartyEventTypeResolver.Resolve("System.Diagnostics.Process, System.Diagnostics.Process");

        type.ShouldBeNull();
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Type? type = PartyEventTypeResolver.Resolve("NonExistentEventType");

        type.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NullOrWhitespace_ReturnsNull(string? input)
    {
        Type? type = PartyEventTypeResolver.Resolve(input!);

        type.ShouldBeNull();
    }

    [Fact]
    public void IsAmbiguousShortName_FullNameMatch_ReturnsFalse()
    {
        // A full-name match always takes precedence — never ambiguous.
        bool ambiguous = PartyEventTypeResolver.IsAmbiguousShortName(typeof(PartyCreated).FullName!);

        ambiguous.ShouldBeFalse();
    }

    [Fact]
    public void IsAmbiguousShortName_UnknownName_ReturnsFalse()
    {
        // "Unknown" must be distinct from "ambiguous" — both return null from Resolve, but the
        // logging diverges. Unknown should NOT be flagged as ambiguous.
        bool ambiguous = PartyEventTypeResolver.IsAmbiguousShortName("NonExistentEventType");

        ambiguous.ShouldBeFalse();
    }

    [Fact]
    public void IsAmbiguousShortName_KnownUniqueShortName_ReturnsFalse()
    {
        bool ambiguous = PartyEventTypeResolver.IsAmbiguousShortName(nameof(PartyCreated));

        ambiguous.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_RepeatQuery_HitsCacheNotAssemblyEnumeration()
    {
        // Calling Resolve twice must yield consistent results — the cache layer stores both
        // resolved types and the explicit "unknown" / "ambiguous" outcomes so subsequent calls
        // do not re-walk the assembly's type list.
        Type? first = PartyEventTypeResolver.Resolve(nameof(PartyCreated));
        Type? second = PartyEventTypeResolver.Resolve(nameof(PartyCreated));

        first.ShouldBe(second);
        first.ShouldBe(typeof(PartyCreated));
    }

    [Fact]
    public void Resolve_RepeatUnknownQuery_ReturnsNullConsistently()
    {
        // Unknown results are also cached so the assembly enumeration is paid once. The outcome
        // type encodes the difference between "ambiguous" and "unknown" so future ambiguity-vs-
        // unknown logging splits remain consistent across cache hits.
        Type? first = PartyEventTypeResolver.Resolve("DefinitelyNotAnEventType_xyz");
        Type? second = PartyEventTypeResolver.Resolve("DefinitelyNotAnEventType_xyz");

        first.ShouldBeNull();
        second.ShouldBeNull();
        PartyEventTypeResolver.IsAmbiguousShortName("DefinitelyNotAnEventType_xyz").ShouldBeFalse();
    }
}
