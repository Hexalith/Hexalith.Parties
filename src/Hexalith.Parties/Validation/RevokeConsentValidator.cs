using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class RevokeConsentValidator : AbstractValidator<RevokeConsent>
{
    public RevokeConsentValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.ConsentId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("ConsentId is required.")
            .Must(ConsentIdentifier.IsValidConsentId)
            .WithMessage("ConsentId must be a support-safe identifier.");
    }
}
