using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Validation;

public sealed class CreatePartyValidator : AbstractValidator<CreateParty>
{
    public CreatePartyValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .NotEqual(PartyType.Unknown)
            .WithMessage("Type must be Person or Organization.");

        When(x => x.Type == PartyType.Person, () =>
        {
            RuleFor(x => x.PersonDetails).NotNull()
                .WithMessage("PersonDetails is required when Type is Person.");
        });

        When(x => x.Type == PartyType.Organization, () =>
        {
            RuleFor(x => x.OrganizationDetails).NotNull()
                .WithMessage("OrganizationDetails is required when Type is Organization.");
        });
    }
}
