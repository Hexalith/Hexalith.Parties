using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Security;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

/// <summary>
/// Verifies the typed <see cref="PartyEncryptionKeyDestroyedException"/> propagation contract:
/// every throw site in the security namespace must emit the typed exception (not the legacy
/// <see cref="InvalidOperationException"/>/<see cref="KeyNotFoundException"/> shape) so callers
/// can recognize the post-erasure condition via <see cref="PartyEncryptionKeyDestroyedException.IsMatch"/>
/// and route to the redaction-fallback path. Without these tests, a refactor that re-introduces
/// a base-type throw would silently break the catch-by-type recognition in the orchestrator.
/// </summary>
public sealed class PartyEncryptionKeyDestroyedExceptionTests
{
    [Fact]
    public void IsMatch_TypedException_ReturnsTrue()
    {
        Exception ex = new PartyEncryptionKeyDestroyedException("acme", "p1");

        PartyEncryptionKeyDestroyedException.IsMatch(ex).ShouldBeTrue();
    }

    [Fact]
    public void IsMatch_GenericKeyNotFoundException_ReturnsFalse()
    {
        // The base class is KeyNotFoundException; tightening the recognition predicate to
        // exact-type match (resolved decision 2) prevents unrelated dictionary-lookup failures
        // from triggering the redaction-fallback path.
        Exception ex = new KeyNotFoundException("Key 'foo' not found");

        PartyEncryptionKeyDestroyedException.IsMatch(ex).ShouldBeFalse();
    }

    [Fact]
    public void IsMatch_InvalidOperationExceptionContainingKeyDestroyedText_ReturnsFalse()
    {
        // The legacy message-text fallback was removed once all throw sites migrated to the
        // typed exception. An unrelated InvalidOperationException whose message coincidentally
        // mentions "key destroyed" must NOT be redacted away.
        Exception ex = new InvalidOperationException("Tenant 'no encryption key holder' violated invariant");

        PartyEncryptionKeyDestroyedException.IsMatch(ex).ShouldBeFalse();
    }

    [Fact]
    public void IsMatch_NullException_ReturnsFalse()
    {
        PartyEncryptionKeyDestroyedException.IsMatch(null).ShouldBeFalse();
    }

    [Fact]
    public void TypedException_CarriesTenantAndPartyContext()
    {
        PartyEncryptionKeyDestroyedException ex = new("acme", "p1");

        ex.TenantId.ShouldBe("acme");
        ex.PartyId.ShouldBe("p1");
        ex.Message.ShouldContain("acme");
        ex.Message.ShouldContain("p1");
    }

    [Fact]
    public void TypedException_PreservesInnerException()
    {
        Exception inner = new TimeoutException("KMS unavailable");
        PartyEncryptionKeyDestroyedException ex = new("acme", "p1", inner);

        ex.InnerException.ShouldBeSameAs(inner);
        ex.TenantId.ShouldBe("acme");
        ex.PartyId.ShouldBe("p1");
    }

    [Fact]
    public async Task PartyKeyManagementService_GetKeyAsync_ThrowsTypedExceptionWhenNoVersions()
    {
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns(Array.Empty<int>());
        IKeyOperationAuditService audit = Substitute.For<IKeyOperationAuditService>();
        ICorrelationContextAccessor correlation = Substitute.For<ICorrelationContextAccessor>();

        PartyKeyManagementService service = new(backend, audit, correlation);

        await Should.ThrowAsync<PartyEncryptionKeyDestroyedException>(
            () => service.GetKeyAsync("acme", "p1", CancellationToken.None));
    }

    [Fact]
    public async Task PartyKeyManagementService_RotateKeyAsync_ThrowsTypedExceptionWhenNoVersions()
    {
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns(Array.Empty<int>());
        IKeyOperationAuditService audit = Substitute.For<IKeyOperationAuditService>();
        ICorrelationContextAccessor correlation = Substitute.For<ICorrelationContextAccessor>();

        PartyKeyManagementService service = new(backend, audit, correlation);

        await Should.ThrowAsync<PartyEncryptionKeyDestroyedException>(
            () => service.RotateKeyAsync("acme", "p1", CancellationToken.None));
    }

    [Fact]
    public async Task PartyKeyManagementService_GetKeyVersionAsync_WrapsRawKeyNotFoundException()
    {
        // Backends that surface a raw KeyNotFoundException (e.g., Vault "Secret not found")
        // must be normalized to the typed exception so catch sites recognize the post-erasure
        // condition via IsMatch(...). Without this normalization, the orchestrator would let
        // the raw exception escape (after the message-text fallback was removed).
        IKeyStorageBackend backend = Substitute.For<IKeyStorageBackend>();
        backend.ReadSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<byte[]>(_ => throw new KeyNotFoundException("Secret not found at path"));
        IKeyOperationAuditService audit = Substitute.For<IKeyOperationAuditService>();
        ICorrelationContextAccessor correlation = Substitute.For<ICorrelationContextAccessor>();

        PartyKeyManagementService service = new(backend, audit, correlation);

        PartyEncryptionKeyDestroyedException ex = await Should.ThrowAsync<PartyEncryptionKeyDestroyedException>(
            () => service.GetKeyVersionAsync("acme", "p1", 1, CancellationToken.None));
        ex.TenantId.ShouldBe("acme");
        ex.PartyId.ShouldBe("p1");
        ex.InnerException.ShouldBeOfType<KeyNotFoundException>();
    }
}
