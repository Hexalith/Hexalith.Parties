using FluentValidation.Results;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Validation;

using Shouldly;

namespace Hexalith.Parties.Tests.Validation;

public sealed class IdentifierValidatorTests
{
    private const string UlidPartyId = "01HYX7QS3NP8M4KQJR5A7CVWKM";
    private const string LegacyGuidPartyId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    [Fact]
    public void PartyIdValidators_AcceptUlidAndLegacyGuid()
    {
        foreach (PartyIdValidatorCase testCase in PartyIdValidatorCases)
        {
            testCase.Validate(UlidPartyId).IsValid.ShouldBeTrue(testCase.Name);
            testCase.Validate(LegacyGuidPartyId).IsValid.ShouldBeTrue(testCase.Name);
        }
    }

    [Fact]
    public void PartyIdValidators_UnsafePartyId_ReturnsSupportSafeFailureWithoutEchoingInput()
    {
        const string unsafeId = "party/unsafe";

        foreach (PartyIdValidatorCase testCase in PartyIdValidatorCases)
        {
            ValidationResult result = testCase.Validate(unsafeId);

            result.IsValid.ShouldBeFalse(testCase.Name);
            result.Errors.ShouldContain(e => e.PropertyName.EndsWith("PartyId", StringComparison.Ordinal)
                && e.ErrorMessage == "PartyId must be a support-safe identifier.");
            result.Errors.ShouldAllBe(e => !e.ErrorMessage.Contains(unsafeId, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("{a1b2c3d4-e5f6-7890-abcd-ef1234567890}")]
    [InlineData("(a1b2c3d4-e5f6-7890-abcd-ef1234567890)")]
    [InlineData("{0xa1b2c3d4,0xe5f6,0x7890,{0xab,0xcd,0xef,0x12,0x34,0x56,0x78,0x90}}")]
    [InlineData(".")]
    [InlineData("---")]
    public void PartyIdValidators_PunctuationOnlyPartyId_ReturnsExpectedResult(string partyId)
    {
        ArgumentNullException.ThrowIfNull(partyId);

        foreach (PartyIdValidatorCase testCase in PartyIdValidatorCases)
        {
            ValidationResult result = testCase.Validate(partyId);

            bool expectedValid = partyId[0] is '{' or '(';
            result.IsValid.ShouldBe(expectedValid, testCase.Name);
            if (!expectedValid)
            {
                result.Errors.ShouldContain(e => e.PropertyName.EndsWith("PartyId", StringComparison.Ordinal)
                    && e.ErrorMessage == "PartyId must be a support-safe identifier.");
            }
        }
    }

    [Fact]
    public void AddIdentifier_ReadableIdentifierId_PassesStandaloneValidation()
    {
        AddIdentifier command = new()
        {
            PartyId = UlidPartyId,
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = "synthetic-vat-value",
        };

        ValidationResult result = new AddIdentifierValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AddIdentifier_InvalidType_ReturnsIdentifierTypeFailure()
    {
        AddIdentifier command = new()
        {
            PartyId = UlidPartyId,
            IdentifierId = "id-vat-1",
            Type = (IdentifierType)999,
            Value = "synthetic-vat-value",
        };

        ValidationResult result = new AddIdentifierValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(AddIdentifier.Type)
            && e.ErrorMessage == "Type must be a valid IdentifierType.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AddIdentifier_EmptyValue_ReturnsValueRequiredFailure(string? value)
    {
        AddIdentifier command = new()
        {
            PartyId = UlidPartyId,
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = value!,
        };

        ValidationResult result = new AddIdentifierValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(AddIdentifier.Value)
            && e.ErrorMessage == "Value is required.");
    }

    [Fact]
    public void RemoveIdentifier_ReadableIdentifierId_PassesStandaloneValidation()
    {
        RemoveIdentifier command = new()
        {
            PartyId = UlidPartyId,
            IdentifierId = "id-vat-1",
        };

        ValidationResult result = new RemoveIdentifierValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CreatePartyComposite_ReadableIdentifierId_PassesCompositeValidation()
    {
        CreatePartyComposite command = new()
        {
            PartyId = UlidPartyId,
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Doe" },
            Identifiers =
            [
                new AddIdentifier
                {
                    PartyId = UlidPartyId,
                    IdentifierId = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "synthetic-vat-value",
                },
            ],
        };

        ValidationResult result = new CreatePartyCompositeValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CreatePartyComposite_MismatchedChildPartyId_ReturnsChildPartyIdFailure()
    {
        CreatePartyComposite command = new()
        {
            PartyId = UlidPartyId,
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Doe" },
            Identifiers =
            [
                new AddIdentifier
                {
                    PartyId = "01HYX7QS3NP8M4KQJR5A7CVWKX",
                    IdentifierId = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "synthetic-vat-value",
                },
            ],
        };

        ValidationResult result = new CreatePartyCompositeValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == "Child PartyId must match PartyId.");
    }

    [Fact]
    public void UpdatePartyComposite_ReadableIdentifierIds_PassCompositeValidation()
    {
        UpdatePartyComposite command = new()
        {
            PartyId = UlidPartyId,
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = UlidPartyId,
                    IdentifierId = "id-siret-1",
                    Type = IdentifierType.SIRET,
                    Value = "synthetic-siret-value",
                },
            ],
            RemoveIdentifierIds = ["id-vat-1"],
        };

        ValidationResult result = new UpdatePartyCompositeValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void UpdatePartyComposite_MismatchedChildPartyId_ReturnsChildPartyIdFailure()
    {
        UpdatePartyComposite command = new()
        {
            PartyId = UlidPartyId,
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = "01HYX7QS3NP8M4KQJR5A7CVWKX",
                    IdentifierId = "id-siret-1",
                    Type = IdentifierType.SIRET,
                    Value = "synthetic-siret-value",
                },
            ],
        };

        ValidationResult result = new UpdatePartyCompositeValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == "Child PartyId must match PartyId.");
    }

    [Fact]
    public void UpdatePartyComposite_UnsafeIdentifierIds_ReturnsSupportSafeFailures()
    {
        UpdatePartyComposite command = new()
        {
            PartyId = UlidPartyId,
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = UlidPartyId,
                    IdentifierId = "id/unsafe",
                    Type = IdentifierType.SIRET,
                    Value = "synthetic-siret-value",
                },
            ],
            RemoveIdentifierIds = ["old:unsafe"],
        };

        ValidationResult result = new UpdatePartyCompositeValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AddIdentifiers[0].IdentifierId"
            && e.ErrorMessage == "IdentifierId must be a support-safe identifier.");
        result.Errors.ShouldContain(e => e.PropertyName == "RemoveIdentifierIds[0]"
            && e.ErrorMessage == "RemoveIdentifierId must be a support-safe identifier.");
        result.Errors.ShouldAllBe(e => !e.ErrorMessage.Contains("id/unsafe", StringComparison.Ordinal)
            && !e.ErrorMessage.Contains("old:unsafe", StringComparison.Ordinal));
    }

    private static IReadOnlyList<PartyIdValidatorCase> PartyIdValidatorCases { get; } =
    [
        new("AddContactChannel", id => new AddContactChannelValidator().Validate(new AddContactChannel
        {
            PartyId = id,
            ContactChannelId = "ch-email-1",
            Type = ContactChannelType.Email,
            Value = "person@example.test",
        })),
        new("AddIdentifier", id => new AddIdentifierValidator().Validate(new AddIdentifier
        {
            PartyId = id,
            IdentifierId = "id-vat-1",
            Type = IdentifierType.VAT,
            Value = "synthetic-vat-value",
        })),
        new("CancelPartyErasure", id => new CancelPartyErasureValidator().Validate(new CancelPartyErasure
        {
            PartyId = id,
            TenantId = "tenant-a",
        })),
        new("CreateParty", id => new CreatePartyValidator().Validate(new CreateParty
        {
            PartyId = id,
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Doe" },
        })),
        new("CreatePartyComposite", id => new CreatePartyCompositeValidator().Validate(new CreatePartyComposite
        {
            PartyId = id,
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Doe" },
        })),
        new("DeactivateParty", id => new DeactivatePartyValidator().Validate(new DeactivateParty { PartyId = id })),
        new("EraseParty", id => new ErasePartyValidator().Validate(new EraseParty
        {
            PartyId = id,
            TenantId = "tenant-a",
        })),
        new("LiftRestriction", id => new LiftRestrictionValidator().Validate(new LiftRestriction
        {
            PartyId = id,
            TenantId = "tenant-a",
        })),
        new("ReactivateParty", id => new ReactivatePartyValidator().Validate(new ReactivateParty { PartyId = id })),
        new("RemoveContactChannel", id => new RemoveContactChannelValidator().Validate(new RemoveContactChannel
        {
            PartyId = id,
            ContactChannelId = "ch-email-1",
        })),
        new("RemoveIdentifier", id => new RemoveIdentifierValidator().Validate(new RemoveIdentifier
        {
            PartyId = id,
            IdentifierId = "id-vat-1",
        })),
        new("RestrictProcessing", id => new RestrictProcessingValidator().Validate(new RestrictProcessing
        {
            PartyId = id,
            TenantId = "tenant-a",
            Reason = "pending data subject request",
        })),
        new("RetryErasureVerification", id => new RetryErasureVerificationValidator().Validate(new RetryErasureVerification
        {
            PartyId = id,
            TenantId = "tenant-a",
        })),
        new("SetIsNaturalPerson", id => new SetIsNaturalPersonValidator().Validate(new SetIsNaturalPerson
        {
            PartyId = id,
            IsNaturalPerson = true,
        })),
        new("UpdateContactChannel", id => new UpdateContactChannelValidator().Validate(new UpdateContactChannel
        {
            PartyId = id,
            ContactChannelId = "ch-email-1",
            Value = "person@example.test",
        })),
        new("UpdateOrganizationDetails", id => new UpdateOrganizationDetailsValidator().Validate(new UpdateOrganizationDetails
        {
            PartyId = id,
            OrganizationDetails = new OrganizationDetails { LegalName = "Acme Corp" },
        })),
        new("UpdatePartyComposite", id => new UpdatePartyCompositeValidator().Validate(new UpdatePartyComposite { PartyId = id })),
        new("UpdatePersonDetails", id => new UpdatePersonDetailsValidator().Validate(new UpdatePersonDetails
        {
            PartyId = id,
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Doe" },
        })),
    ];

    private sealed record PartyIdValidatorCase(string Name, Func<string, ValidationResult> Validate)
    {
        public override string ToString() => Name;
    }
}
