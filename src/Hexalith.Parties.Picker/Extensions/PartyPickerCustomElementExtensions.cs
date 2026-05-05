using Hexalith.Parties.Picker.Components;
using Hexalith.Parties.Picker.Services;

using Microsoft.AspNetCore.Components.Web;

namespace Hexalith.Parties.Picker.Extensions;

public static class PartyPickerCustomElementExtensions
{
    public static IJSComponentConfiguration RegisterHexalithPartyPickerCustomElement(
        this IJSComponentConfiguration jsComponents,
        string elementName = PartyPickerDefaults.CustomElementName)
    {
        ArgumentNullException.ThrowIfNull(jsComponents);
        jsComponents.RegisterCustomElement<PartyPicker>(elementName);
        return jsComponents;
    }
}
