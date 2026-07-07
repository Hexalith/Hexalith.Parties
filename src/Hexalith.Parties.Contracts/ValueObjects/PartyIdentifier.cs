namespace Hexalith.Parties.Contracts.ValueObjects;

public sealed record PartyIdentifier
{
    /// <summary>
    /// Maximum supported length for a Parties semantic identifier.
    /// </summary>
    public const int MaximumSemanticIdLength = 128;

    public required string Id { get; init; }

    public required IdentifierType Type { get; init; }

    [PersonalData]
    public required string Value { get; init; }

    public string? Jurisdiction { get; init; }

    /// <summary>
    /// Determines whether the specified value is safe to use as a Parties semantic identifier.
    /// </summary>
    /// <param name="value">The identifier value to validate.</param>
    /// <returns><see langword="true"/> when the value is non-empty, bounded, and support-safe; otherwise <see langword="false"/>.</returns>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumSemanticIdLength)
        {
            return false;
        }

        if (IsBracketedLegacyGuid(value) || IsLegacyGuidXFormat(value))
        {
            return true;
        }

        if (!IsAlphaNumeric(value[0]) || !IsAlphaNumeric(value[^1]))
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (!IsAllowed(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowed(char ch)
        => IsAlphaNumeric(ch)
            || ch is '.' or '_' or '-';

    private static bool IsAlphaNumeric(char ch)
        => ch is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9';

    private static bool IsBracketedLegacyGuid(string value)
    {
        if (value.Length != 38
            || !((value[0] == '{' && value[^1] == '}')
                || (value[0] == '(' && value[^1] == ')')))
        {
            return false;
        }

        for (int i = 1; i < value.Length - 1; i++)
        {
            char ch = value[i];
            bool expectedDash = i is 9 or 14 or 19 or 24;
            if (expectedDash)
            {
                if (ch != '-')
                {
                    return false;
                }

                continue;
            }

            if (!IsHex(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLegacyGuidXFormat(string value)
    {
        if (value.Length != 68 || value[0] != '{' || value[^1] != '}')
        {
            return false;
        }

        int index = 1;
        if (!ConsumeHexPrefix(value, ref index, 8)
            || !Consume(value, ref index, ',')
            || !ConsumeHexPrefix(value, ref index, 4)
            || !Consume(value, ref index, ',')
            || !ConsumeHexPrefix(value, ref index, 4)
            || !Consume(value, ref index, ',')
            || !Consume(value, ref index, '{'))
        {
            return false;
        }

        for (int i = 0; i < 8; i++)
        {
            if (!ConsumeHexPrefix(value, ref index, 2))
            {
                return false;
            }

            if (i < 7 && !Consume(value, ref index, ','))
            {
                return false;
            }
        }

        return Consume(value, ref index, '}') && index == value.Length - 1;
    }

    private static bool Consume(string value, ref int index, char expected)
    {
        if (index >= value.Length || value[index] != expected)
        {
            return false;
        }

        index++;
        return true;
    }

    private static bool ConsumeHexPrefix(string value, ref int index, int hexDigits)
    {
        if (index + 2 + hexDigits > value.Length
            || value[index] != '0'
            || value[index + 1] is not ('x' or 'X'))
        {
            return false;
        }

        index += 2;
        for (int i = 0; i < hexDigits; i++)
        {
            if (!IsHex(value[index + i]))
            {
                return false;
            }
        }

        index += hexDigits;
        return true;
    }

    private static bool IsHex(char ch)
        => ch is >= 'A' and <= 'F'
            or >= 'a' and <= 'f'
            or >= '0' and <= '9';
}
