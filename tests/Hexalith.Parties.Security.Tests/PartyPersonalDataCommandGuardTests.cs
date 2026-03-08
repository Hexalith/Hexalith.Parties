using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.Security;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Security;

using NSubstitute;

using Shouldly;

namespace Hexalith.Parties.Security.Tests;

public sealed class PartyPersonalDataCommandGuardTests
{
    private readonly ICryptoStatusProvider _cryptoStatusProvider = Substitute.For<ICryptoStatusProvider>();
    private readonly IKeyStorageBackend _keyStorageBackend = Substitute.For<IKeyStorageBackend>();

    private PartyPersonalDataCommandGuard CreateGuard() => new(_cryptoStatusProvider, _keyStorageBackend);

    [Fact]
    public async Task GetBlockingReasonAsync_PendingCryptoForPersonUpdate_ReturnsReason()
    {
        _cryptoStatusProvider.IsCryptoPendingAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns(true);

        UpdatePersonDetails command = new()
        {
            PartyId = "p1",
            PersonDetails = new PersonDetails
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                DateOfBirth = null,
                Prefix = null,
                Suffix = null,
            },
        };

        string? reason = await CreateGuard().GetBlockingReasonAsync("acme", "p1", command);

        reason.ShouldNotBeNull();
        reason.ShouldContain("CryptoPending");
    }

    [Fact]
    public async Task GetBlockingReasonAsync_NonNaturalPersonOrganizationUpdate_AllowsWriteWithoutKey()
    {
        UpdateOrganizationDetails command = new()
        {
            PartyId = "org1",
            OrganizationDetails = new OrganizationDetails
            {
                LegalName = "Acme Corp",
                TradingName = "Acme",
                LegalForm = "SAS",
                RegistrationNumber = "123456",
                IsNaturalPerson = false,
            },
        };

        string? reason = await CreateGuard().GetBlockingReasonAsync("acme", "org1", command);

        reason.ShouldBeNull();
        await _cryptoStatusProvider.DidNotReceive().IsCryptoPendingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBlockingReasonAsync_AddIdentifierWithoutKey_ReturnsReason()
    {
        _cryptoStatusProvider.IsCryptoPendingAsync("acme", "p1", Arg.Any<CancellationToken>()).Returns(false);
        _keyStorageBackend.ListKeyVersionsAsync("acme", "p1", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<int>)[]);

        AddIdentifier command = new()
        {
            PartyId = "p1",
            IdentifierId = Guid.NewGuid().ToString(),
            Type = IdentifierType.VAT,
            Value = "FR123456789",
        };

        string? reason = await CreateGuard().GetBlockingReasonAsync("acme", "p1", command);

        reason.ShouldNotBeNull();
        reason.ShouldContain("no party encryption key", Case.Insensitive);
    }
}