using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class RetryErasureVerificationValidator : AbstractValidator<RetryErasureVerification>
{
    public RetryErasureVerificationValidator()
    {
        // Explicit evidence recovery retains the same tenant/party boundary as a normal retry.
        // The boolean opt-in is false when omitted; JSON rejects non-boolean wire values.
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");
    }
}
