namespace Hexalith.Parties.Contracts.ValueObjects;

/// <summary>
/// Provides compatibility-oriented validation for consent identifiers.
/// </summary>
public static class ConsentIdentifier
{
    private const int MaximumPurposeLength = 100;

    private const int MaximumConsentIdLength
        = PartyIdentifier.MaximumSemanticIdLength + 1 + MaximumPurposeLength;

    /// <summary>
    /// Determines whether the specified value is a compatible contact-channel identifier.
    /// </summary>
    /// <param name="value">The contact-channel identifier to validate.</param>
    /// <returns><see langword="true"/> when the value satisfies the Parties semantic-ID contract; otherwise <see langword="false"/>.</returns>
    public static bool IsValidChannelId(string? value) => PartyIdentifier.IsValid(value);

    /// <summary>
    /// Determines whether the specified value is a compatible consent identifier.
    /// </summary>
    /// <param name="value">The consent identifier to validate without normalization.</param>
    /// <returns><see langword="true"/> when the value is an opaque semantic ID or a valid channel-purpose composite; otherwise <see langword="false"/>.</returns>
    public static bool IsValidConsentId(string? value)
    {
        if (PartyIdentifier.IsValid(value))
        {
            return true;
        }

        if (string.IsNullOrEmpty(value) || value.Length > MaximumConsentIdLength)
        {
            return false;
        }

        int separatorIndex = value.IndexOf(':');
        return separatorIndex > 0
            && separatorIndex == value.LastIndexOf(':')
            && separatorIndex < value.Length - 1
            && IsValidChannelId(value[..separatorIndex])
            && IsValidPurpose(value[(separatorIndex + 1)..]);
    }

    /// <summary>
    /// Determines whether the specified value is a compatible consent-purpose segment.
    /// </summary>
    /// <param name="value">The consent-purpose segment to validate.</param>
    /// <returns><see langword="true"/> when the value contains 1 to 100 permitted ASCII characters; otherwise <see langword="false"/>.</returns>
    public static bool IsValidPurpose(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumPurposeLength)
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (ch is not (>= 'A' and <= 'Z')
                and not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9')
                and not '-'
                and not '_')
            {
                return false;
            }
        }

        return true;
    }
}
