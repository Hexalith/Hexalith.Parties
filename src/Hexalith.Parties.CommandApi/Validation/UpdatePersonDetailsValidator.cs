using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.CommandApi.Validation;

public sealed class UpdatePersonDetailsValidator : AbstractValidator<UpdatePersonDetails>
{
    public UpdatePersonDetailsValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.PersonDetails)
            .NotNull()
            .WithMessage("PersonDetails is required.");
    }
}
