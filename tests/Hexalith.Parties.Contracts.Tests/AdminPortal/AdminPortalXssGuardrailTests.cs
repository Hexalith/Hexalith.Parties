// ATDD red-phase XSS guardrail scaffolds for Story 10.1 — Admin Portal.
// AC6: rendered party fields may contain user-supplied or AI-created data. The portal
// must output-encode every field via standard Razor binding and never use MarkupString,
// AddMarkupContent, raw HTML fragments, or untrusted-data JS interop bridges. These
// fitness checks scan the AdminPortal assembly for forbidden API usage; they remain
// skipped until the assembly exists in green phase.

using System.Linq;
using System.Reflection;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.AdminPortal;

/// <summary>
/// Story 10.1 — AC6 + Implementation Guardrails. Reflective scans that prevent the admin
/// portal from rendering party data through unsafe Razor seams.
/// </summary>
public sealed class AdminPortalXssGuardrailTests
{
    private const string SkipReason =
        "TDD red phase — Hexalith.Parties.AdminPortal assembly not yet added by Story 10.1.";

    private const string AdminPortalAssemblyName = "Hexalith.Parties.AdminPortal";
    private const string MarkupStringFullName = "Microsoft.AspNetCore.Components.MarkupString";

    private static readonly string[] _forbiddenRenderApis =
    [
        "AddMarkupContent",
        "AddContent.*MarkupString",
    ];

    [Fact(Skip = SkipReason)]
    public void AdminPortal_DoesNotDeclareMarkupStringFields()
    {
        // AC6: forbid any field/property typed as MarkupString — reduces the surface where a
        // future maintainer can accidentally bind unencoded party data into the rendered DOM.
        Assembly portal = LoadPortalAssembly();

        IEnumerable<MemberInfo> markupCarriers = portal.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Cast<MemberInfo>()
                .Concat(t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
            .Where(IsMarkupStringMember);

        markupCarriers.ShouldBeEmpty(
            "AdminPortal must not declare MarkupString fields/properties for party data rendering.");
    }

    [Fact(Skip = SkipReason)]
    public void AdminPortal_DoesNotInvokeAddMarkupContentInBuildRenderTree()
    {
        // AC6: detect AddMarkupContent calls in BuildRenderTree IL. Story 10.1 must use only
        // standard Razor encoding (AddContent with strings or framework primitives).
        Assembly portal = LoadPortalAssembly();

        IEnumerable<MethodInfo> renderMethods = portal.GetTypes()
            .Where(t => t.GetMethod("BuildRenderTree", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is not null)
            .Select(t => t.GetMethod("BuildRenderTree", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!);

        IEnumerable<string> offenders = renderMethods
            .Where(m => m.GetMethodBody() is not null)
            .Where(ContainsForbiddenIlReference)
            .Select(m => m.DeclaringType?.FullName ?? m.Name);

        offenders.ShouldBeEmpty(
            "AdminPortal components must not call AddMarkupContent or render MarkupString from BuildRenderTree.");
    }

    [Fact(Skip = SkipReason)]
    public void AdminPortal_HasNoJsInteropBridgeForRawPartyHtml()
    {
        // Implementation Guardrails: do not pipe party values through JS interop scripts
        // that could reintroduce stored XSS by setting innerHTML or eval-equivalent paths.
        Assembly portal = LoadPortalAssembly();

        string[] forbiddenInteropMembers =
        [
            "innerHTML",
            "outerHTML",
            "eval",
            "setHtmlUnsafe",
        ];

        IEnumerable<string> stringConstants = portal.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string?)f.GetRawConstantValue() ?? string.Empty);

        foreach (string literal in stringConstants)
        {
            foreach (string forbidden in forbiddenInteropMembers)
            {
                literal.Contains(forbidden, StringComparison.Ordinal)
                    .ShouldBeFalse($"AdminPortal exposes a JS interop literal containing '{forbidden}'.");
            }
        }
    }

    private static bool IsMarkupStringMember(MemberInfo member)
    {
        Type? memberType = member switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => null,
        };

        return memberType?.FullName == MarkupStringFullName;
    }

    private static bool ContainsForbiddenIlReference(MethodInfo method)
    {
        // Lightweight IL scan: look for tokens whose resolved member name matches the
        // forbidden render APIs. Skipped tests do not run; activation populates the
        // resolver paths once Microsoft.AspNetCore.Components.RenderTree is loaded.
        MethodBody? body = method.GetMethodBody();
        if (body is null)
        {
            return false;
        }

        Module module = method.Module;
        byte[] il = body.GetILAsByteArray() ?? [];

        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] is 0x28 or 0x6F)
            {
                int token = BitConverter.ToInt32(il, i + 1);
                try
                {
                    MemberInfo? resolved = module.ResolveMember(token);
                    string? name = resolved?.Name;
                    if (name is not null && _forbiddenRenderApis.Any(forbidden =>
                        name.Contains(forbidden.Split('.', 2)[0], StringComparison.Ordinal)))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Token resolution failures are ignored during IL scan.
                }

                i += 4;
            }
        }

        return false;
    }

    private static Assembly LoadPortalAssembly()
    {
        Assembly? loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, AdminPortalAssemblyName, StringComparison.Ordinal));

        return loaded ?? Assembly.Load(new AssemblyName(AdminPortalAssemblyName));
    }
}
