using Hexalith.Memories.Client.Rest;
using Hexalith.Memories.Contracts.V1;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hexalith.Parties.Tests.Search;

/// <summary>Exposes controlled acceptance boundaries while exercising the real SDK status client.</summary>
internal sealed class PartyMemoryRecoveryClient(HttpClient http) : MemoriesClient(http, Options.Create(new MemoriesClientOptions()), NullLogger<MemoriesClient>.Instance)
{
    public int Submissions { get; private set; }

    public string? Token { get; private set; }

    public byte[]? Content { get; private set; }

    public Func<CancellationToken, Task<string>> Submit { get; set; } = _ => Task.FromResult("workflow");

    public override Task<string> IngestAsync(string tenantId, string caseId, string sourceUri, byte[] content, string contentType,
        string ingestedBy, IReadOnlyDictionary<string, MetadataField>? metadata, string? idempotencyToken, CancellationToken ct)
    {
        Submissions++;
        Token = idempotencyToken;
        Content = content;
        return Submit(ct);
    }
}
