using FluentValidation.Results;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Validation;

using Shouldly;

namespace Hexalith.Parties.Tests.Validation;

public sealed class IdentifierValidatorTests
{
    [Fact]
    public void AddIdentifier_ReadableIdentifierId_PassesStandaloneValidation()
    {
        AddIdentifier command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
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
            PartyId = Guid.NewGuid().ToString("D"),
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
            PartyId = Guid.NewGuid().ToString("D"),
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
            PartyId = Guid.NewGuid().ToString("D"),
            IdentifierId = "id-vat-1",
        };

        ValidationResult result = new RemoveIdentifierValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CreatePartyComposite_ReadableIdentifierId_ReturnsGuidFailure()
    {
        CreatePartyComposite command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            Type = PartyType.Person,
            PersonDetails = new PersonDetails { FirstName = "Jane", LastName = "Doe" },
            Identifiers =
            [
                new AddIdentifier
                {
                    PartyId = Guid.NewGuid().ToString("D"),
                    IdentifierId = "id-vat-1",
                    Type = IdentifierType.VAT,
                    Value = "synthetic-vat-value",
                },
            ],
        };

        ValidationResult result = new CreatePartyCompositeValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Identifiers[0].IdentifierId"
            && e.ErrorMessage == "IdentifierId must be a valid GUID.");
    }

    [Fact]
    public void UpdatePartyComposite_ReadableIdentifierId_ReturnsGuidFailure()
    {
        UpdatePartyComposite command = new()
        {
            PartyId = Guid.NewGuid().ToString("D"),
            AddIdentifiers =
            [
                new AddIdentifier
                {
                    PartyId = Guid.NewGuid().ToString("D"),
                    IdentifierId = "id-siret-1",
                    Type = IdentifierType.SIRET,
                    Value = "synthetic-siret-value",
                },
            ],
            RemoveIdentifierIds = ["id-vat-1"],
        };

        ValidationResult result = new UpdatePartyCompositeValidator().Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "AddIdentifiers[0].IdentifierId"
            && e.ErrorMessage == "IdentifierId must be a valid GUID.");
        result.Errors.ShouldContain(e => e.PropertyName == "RemoveIdentifierIds[0]"
            && e.ErrorMessage == "RemoveIdentifierId must be a valid GUID.");
    }
}
