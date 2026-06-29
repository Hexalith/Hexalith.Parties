namespace Hexalith.Parties.Security.Tests;

internal static class ProtectedDataLeakSentinel
{
    public const string ProtectedPayloadPlaintext = "PROTECTED_PAYLOAD_PLAINTEXT_MARKER_7_6";

    public const string ProtectedSnapshotPlaintext = "PROTECTED_SNAPSHOT_PLAINTEXT_MARKER_7_6";

    public const string ProtectedKeyAlias = "PROTECTED_KEY_ALIAS_MARKER_7_6";

    public const string ProtectedProviderPrivateBlob = "PROTECTED_PROVIDER_PRIVATE_BLOB_MARKER_7_6";

    public const string ProtectedStateStoreKey = "PROTECTED_STATE_STORE_KEY_MARKER_7_6";

    public const string ProtectedConnectionString = "PROTECTED_CONNECTION_STRING_MARKER_7_6";

    public const string ProtectedProviderExceptionText = "PROTECTED_PROVIDER_EXCEPTION_MARKER_7_6";

    public static IReadOnlyList<string> All() =>
    [
        ProtectedPayloadPlaintext,
        ProtectedSnapshotPlaintext,
        ProtectedKeyAlias,
        ProtectedProviderPrivateBlob,
        ProtectedStateStoreKey,
        ProtectedConnectionString,
        ProtectedProviderExceptionText,
    ];

    public static void AssertNoLeak(IEnumerable<string?> captured)
    {
        ArgumentNullException.ThrowIfNull(captured);

        IReadOnlyList<string> sentinels = All();
        foreach (string? entry in captured)
        {
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }

            for (int i = 0; i < sentinels.Count; i++)
            {
                if (entry.Contains(sentinels[i], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Protected-data sentinel at index {i} was found in captured output.");
                }
            }
        }
    }
}
