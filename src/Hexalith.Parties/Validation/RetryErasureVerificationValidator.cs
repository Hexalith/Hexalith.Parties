using FluentValidation;

using Hexalith.Parties.Contracts.Commands;

namespace Hexalith.Parties.Validation;

public sealed class RetryErasureVerificationValidator : AbstractValidator<RetryErasureVerification>
{
    public RetryErasureVerificationValidator()
    {
        RuleFor(x => x.PartyId)
            .NotEmpty()
            .Must(id => Guid.TryParse(id, out _))
            .WithMessage("PartyId must be a valid GUID.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");
    }
}
