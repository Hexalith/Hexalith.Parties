using System.Globalization;

using Hexalith.Parties.Contracts;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests;

public sealed class PartyDisplayFormatTests
{
    [Fact]
    public void FormatDate_PreservesAdminCompactDateStyle()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("fr-FR");
        var value = new DateTimeOffset(2026, 6, 29, 16, 45, 0, TimeSpan.FromHours(2));

        string actual = PartyDisplayFormat.FormatDate(value, culture, PartyDisplayFormat.CompactDateFormat);

        actual.ShouldBe(value.ToString("g", culture));
    }

    [Fact]
    public void FormatDate_PreservesConsumerPlainDateStyle()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
        var value = new DateTimeOffset(2026, 6, 29, 16, 45, 0, TimeSpan.FromHours(2));

        string actual = PartyDisplayFormat.FormatDate(value, culture, PartyDisplayFormat.PlainDateFormat);

        actual.ShouldBe(value.ToString("d", culture));
    }

    [Fact]
    public void FormatBoolean_UsesLocalizedCallerProvidedLabels()
    {
        PartyDisplayFormat.FormatBoolean(true, "Oui", "Non").ShouldBe("Oui");
        PartyDisplayFormat.FormatBoolean(false, "Oui", "Non").ShouldBe("Non");
    }

    [Fact]
    public void FormatDate_WithNullableMissingValue_UsesCallerProvidedLabel()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");

        PartyDisplayFormat
            .FormatDate(null, culture, PartyDisplayFormat.PlainDateFormat, "Not provided")
            .ShouldBe("Not provided");

        PartyDisplayFormat
            .FormatDate(default(DateTimeOffset), culture, PartyDisplayFormat.PlainDateFormat, "Not provided")
            .ShouldBe("Not provided");
    }
}
