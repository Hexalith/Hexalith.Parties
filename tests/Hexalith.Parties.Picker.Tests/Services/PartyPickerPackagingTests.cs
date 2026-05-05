using Hexalith.Parties.Contracts.Models;
using Hexalith.Parties.Picker.Components;
using Hexalith.Parties.Picker.Extensions;
using Hexalith.Parties.Picker.Services;

using Shouldly;

namespace Hexalith.Parties.Picker.Tests.Services;

public sealed class PartyPickerPackagingTests
{
    [Fact]
    public void ContractsAssembly_DoesNotReferencePickerOrBlazorUiPackages()
    {
        string[] references = typeof(PartyIndexEntry).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .ToArray();

        references.ShouldNotContain("Hexalith.Parties.Picker");
        references.ShouldNotContain("Microsoft.FluentUI.AspNetCore.Components");
        references.ShouldNotContain("Microsoft.AspNetCore.Components.CustomElements");
    }

    [Fact]
    public void PickerPackage_ExposesComponentAndCustomElementRegistrationSurface()
    {
        typeof(PartyPicker).IsPublic.ShouldBeTrue();
        typeof(PartyPickerCustomElementExtensions).IsPublic.ShouldBeTrue();
        PartyPickerDefaults.CustomElementName.ShouldBe("hexalith-party-picker");
        PartyPickerDefaults.DomEventName.ShouldBe("party-selected");
    }

    [Fact]
    public void DomEventPayload_DoesNotExposeSearchTextOrPersonalContactFields()
    {
        string[] properties = typeof(PartyPickerEventDetail)
            .GetProperties()
            .Select(static property => property.Name)
            .ToArray();

        properties.ShouldBe(["PartyId", "PartyType", "Status"], ignoreOrder: true);
    }
}
