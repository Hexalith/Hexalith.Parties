using System.Globalization;

namespace Hexalith.Parties.Contracts;

public static class PartyDisplayFormat
{
    public const string CompactDateFormat = "g";

    public const string PlainDateFormat = "d";

    public static string FormatBoolean(bool value, string trueLabel, string falseLabel)
    {
        ArgumentNullException.ThrowIfNull(trueLabel);
        ArgumentNullException.ThrowIfNull(falseLabel);

        return value ? trueLabel : falseLabel;
    }

    public static string FormatDate(DateTimeOffset value, CultureInfo culture, string format)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        return value.ToString(format, culture);
    }

    public static string FormatDate(DateTimeOffset? value, CultureInfo culture, string format, string missingLabel)
    {
        ArgumentNullException.ThrowIfNull(missingLabel);

        return value is { } date && date != default
            ? FormatDate(date, culture, format)
            : missingLabel;
    }
}
