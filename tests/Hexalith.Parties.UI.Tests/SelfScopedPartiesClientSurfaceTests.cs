using System.Reflection;

using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.UI.Services;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

/// <summary>
/// Story 1.5 AC6 (TRIPWIRE) — the AC1 "a Consumer never calls list/search" guarantee made structural.
/// Reflection over <see cref="ISelfScopedPartiesClient"/> asserts the surface can never expose a
/// list/search-shaped member (a <c>List*</c>/<c>Search*</c> name, or a <see cref="PagedResult{T}"/>
/// return) and never accepts a <c>partyId</c> (the accessor injects the resolved id). If a future edit
/// adds such a member, this test fails the build — not a login.
/// </summary>
public sealed class SelfScopedPartiesClientSurfaceTests
{
    [Fact]
    public void Accessor_ExposesNoListOrSearchNamedMember()
    {
        string[] offenders = typeof(ISelfScopedPartiesClient).GetMethods()
            .Where(m => m.Name.StartsWith("List", StringComparison.Ordinal)
                || m.Name.StartsWith("Search", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToArray();

        offenders.ShouldBeEmpty(
            "Consumer self-scope accessor must never expose list/search. Offending members: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Accessor_ExposesNoPagedResultReturningMember()
    {
        string[] offenders = typeof(ISelfScopedPartiesClient).GetMethods()
            .Where(m => IsPagedResultShaped(UnwrapTask(m.ReturnType)))
            .Select(m => m.Name)
            .ToArray();

        offenders.ShouldBeEmpty(
            "Consumer self-scope accessor must never return a paged (list/search) result. Offending members: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Accessor_AcceptsNoPartyIdParameter()
    {
        string[] offenders = typeof(ISelfScopedPartiesClient).GetMethods()
            .Where(m => m.GetParameters().Any(p =>
                p.Name is not null && p.Name.Contains("partyId", StringComparison.OrdinalIgnoreCase)))
            .Select(m => m.Name)
            .ToArray();

        offenders.ShouldBeEmpty(
            "Consumer self-scope accessor must inject the resolved party_id, never accept one. Offending members: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Accessor_AcceptsNoParameterTypeWithPartyIdProperty()
    {
        string[] offenders = typeof(ISelfScopedPartiesClient).GetMethods()
            .SelectMany(static method => method.GetParameters().Select(parameter => new { method.Name, ParameterType = parameter.ParameterType }))
            .Where(static parameter => parameter.ParameterType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(static property => property.Name.Contains("PartyId", StringComparison.OrdinalIgnoreCase)))
            .Select(static parameter => parameter.Name)
            .Distinct()
            .ToArray();

        offenders.ShouldBeEmpty(
            "Consumer self-scope accessor request types must not expose party id shaped properties. Offending members: "
            + string.Join(", ", offenders));
    }

    private static Type UnwrapTask(Type returnType)
        => returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : returnType;

    private static bool IsPagedResultShaped(Type type)
    {
        Type candidate = Nullable.GetUnderlyingType(type) ?? type;
        return candidate.IsGenericType
            && candidate.GetGenericTypeDefinition() == typeof(PagedResult<>);
    }
}
