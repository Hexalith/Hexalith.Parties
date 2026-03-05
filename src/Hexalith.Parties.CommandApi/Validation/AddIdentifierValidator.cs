using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.CommandApi.Validation;

public sealed class AddIdentifierValidator : AbstractValidator<AddIdentifier>
{
    public AddIdentifierValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.IdentifierId)
            .NotEmpty()
            .WithMessage("IdentifierId is required.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Type must be a valid IdentifierType.");

        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Value is required.");
    }
}
