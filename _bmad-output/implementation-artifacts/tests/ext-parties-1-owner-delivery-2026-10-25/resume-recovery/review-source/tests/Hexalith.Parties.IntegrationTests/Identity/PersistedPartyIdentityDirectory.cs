using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Parties.Contracts;

namespace Hexalith.Parties.IntegrationTests.Identity;

/// <summary>Disposable directory of party streams. Restarting a host reloads the same files.</summary>
internal sealed class PersistedPartyIdentityDirectory
{
    private readonly Lock _gate = new();

    /// <summary>Creates a store rooted at <paramref name="root"/>.</summary>
    /// <param name="root">The disposable directory.</param>
    public PersistedPartyIdentityDirectory(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = root;
        Directory.CreateDirectory(root);
    }

    /// <summary>Gets the directory that survives a host restart.</summary>
    public string Root { get; }

    /// <summary>Gets or sets a read fault applied to the next authoritative read: outage, gap, future, or stale.</summary>
    public string? ReadFault { get; set; }

    /// <summary>Gets the number of authoritative reads since this process loaded the directory.</summary>
    public int AuthoritativeReads { get; private set; }

    /// <summary>Gets the number of retained-history reads since this process loaded the directory.</summary>
    public int RetainedReads { get; private set; }

    /// <summary>Records an authoritative read.</summary>
    public void CountAuthoritativeRead()
    {
        lock (_gate)
        {
            AuthoritativeReads++;
        }
    }

    /// <summary>Records a retained-history read.</summary>
    public void CountRetainedRead()
    {
        lock (_gate)
        {
            RetainedReads++;
        }
    }

    /// <summary>Appends one plaintext profile event.</summary>
    public void AppendPlain(string tenantId, string partyId, string eventTypeName, byte[] payload, DateTimeOffset timestamp)
        => Append(tenantId, partyId, eventTypeName, payload, protectedEvent: false, timestamp, evidenceId: null, sealedDigest: null);

    /// <summary>Appends one protected history event.</summary>
    public void AppendProtected(string tenantId, string partyId, string eventTypeName, byte[] payload, DateTimeOffset timestamp, string evidenceId, string sealedDigest)
        => Append(tenantId, partyId, eventTypeName, payload, protectedEvent: true, timestamp, evidenceId, sealedDigest);

    /// <summary>Reads the durable events for one party.</summary>
    public IReadOnlyList<PersistedPartyEvent> Read(string tenantId, string partyId)
    {
        lock (_gate)
        {
            return Load(tenantId, partyId);
        }
    }

    /// <summary>Hashes the durable stream bytes so a denied caller can be shown not to have changed them.</summary>
    public string Fingerprint(string tenantId, string partyId)
    {
        lock (_gate)
        {
            string path = StreamPath(tenantId, partyId);
            byte[] bytes = File.Exists(path) ? File.ReadAllBytes(path) : [];
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
    }

    /// <summary>Persists a retained partition that the history reader serves after a restart.</summary>
    public void SaveRetainedOverride(string tenantId, string partyId, RetainedIdentityHistoryStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        lock (_gate)
        {
            string path = OverridePath(tenantId, partyId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(stream, PartiesJsonOptions.Default));
        }
    }

    /// <summary>Loads a retained override, or null when the readable partition is derived from the stream.</summary>
    public RetainedIdentityHistoryStream? ReadRetainedOverride(string tenantId, string partyId)
    {
        lock (_gate)
        {
            string path = OverridePath(tenantId, partyId);
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<RetainedIdentityHistoryStream>(File.ReadAllBytes(path), PartiesJsonOptions.Default);
        }
    }

    private void Append(string tenantId, string partyId, string eventTypeName, byte[] payload, bool protectedEvent, DateTimeOffset timestamp, string? evidenceId, string? sealedDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ArgumentNullException.ThrowIfNull(payload);
        lock (_gate)
        {
            List<PersistedPartyEvent> events = Load(tenantId, partyId);
            events.Add(new PersistedPartyEvent(events.Count + 1, eventTypeName, payload.ToArray(), protectedEvent, timestamp, evidenceId, sealedDigest));
            Write(tenantId, partyId, events);
        }
    }

    private List<PersistedPartyEvent> Load(string tenantId, string partyId)
    {
        string path = StreamPath(tenantId, partyId);
        if (!File.Exists(path))
        {
            return [];
        }

        StoredDocument? document = JsonSerializer.Deserialize<StoredDocument>(File.ReadAllBytes(path));
        if (document?.Events is null)
        {
            return [];
        }

        return document.Events.Select(item => new PersistedPartyEvent(
            item.Sequence,
            item.EventTypeName,
            Convert.FromBase64String(item.PayloadBase64),
            item.Protected,
            item.Timestamp,
            item.EvidenceId,
            item.SealedDigest)).ToList();
    }

    private void Write(string tenantId, string partyId, IReadOnlyList<PersistedPartyEvent> events)
    {
        StoredDocument document = new()
        {
            Events = events.Select(item => new StoredEvent
            {
                Sequence = item.Sequence,
                EventTypeName = item.EventTypeName,
                PayloadBase64 = Convert.ToBase64String(item.Payload),
                Protected = item.Protected,
                Timestamp = item.Timestamp,
                EvidenceId = item.EvidenceId,
                SealedDigest = item.SealedDigest,
            }).ToList(),
        };
        string path = StreamPath(tenantId, partyId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteAtomic(path, JsonSerializer.SerializeToUtf8Bytes(document));
    }

    private string StreamPath(string tenantId, string partyId) => Path.Combine(Root, "streams", tenantId, partyId + ".json");

    private string OverridePath(string tenantId, string partyId) => Path.Combine(Root, "retained", tenantId, partyId + ".json");

    private static void WriteAtomic(string path, byte[] bytes)
    {
        string temporary = path + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
    }

    private sealed class StoredDocument
    {
        public List<StoredEvent> Events { get; set; } = [];
    }

    private sealed class StoredEvent
    {
        public long Sequence { get; set; }

        public string EventTypeName { get; set; } = string.Empty;

        public string PayloadBase64 { get; set; } = string.Empty;

        public bool Protected { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public string? EvidenceId { get; set; }

        public string? SealedDigest { get; set; }
    }
}
