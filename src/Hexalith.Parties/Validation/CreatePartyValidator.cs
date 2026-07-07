using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class CreatePartyValidator : AbstractValidator<CreateParty>
{
    public CreatePartyValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

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
