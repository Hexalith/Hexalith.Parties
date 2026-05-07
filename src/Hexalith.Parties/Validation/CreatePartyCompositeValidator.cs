using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;
using Hexalith.Parties.Server.Aggregates;

namespace Hexalith.Parties.Validation;

public sealed class CreatePartyCompositeValidator : AbstractValidator<CreatePartyComposite>
{
    public CreatePartyCompositeValidator()
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
            RuleFor(x => x.PersonDetails)
                .NotNull()
                .WithMessage("PersonDetails is required when Type is Person.");
        });

        When(x => x.Type == PartyType.Organization, () =>
        {
            RuleFor(x => x.OrganizationDetails)
                .NotNull()
                .WithMessage("OrganizationDetails is required when Type is Organization.");
        });

        RuleFor(x => x)
            .Must(x => CountSubOperations(x) <= PartyAggregate.GetEffectiveMaxSubOperations())
            .WithMessage(x => $"Total sub-operations ({CountSubOperations(x)}) exceeds maximum ({PartyAggregate.GetEffectiveMaxSubOperations()}).");

        RuleForEach(x => x.ContactChannels).ChildRules(channel =>
        {
            channel.RuleFor(c => c.PartyId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("PartyId must be a valid GUID.");

            channel.RuleFor(c => c.ContactChannelId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("ContactChannelId must be a valid GUID.");

            channel.RuleFor(c => c.Type)
                .IsInEnum()
                .WithMessage("Type must be a valid ContactChannelType.");

            channel.RuleFor(c => c.Value)
                .NotEmpty()
                .WithMessage("Value is required.");
        });

        RuleForEach(x => x.Identifiers).ChildRules(identifier =>
        {
            identifier.RuleFor(i => i.PartyId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("PartyId must be a valid GUID.");

            identifier.RuleFor(i => i.IdentifierId)
                .NotEmpty()
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("IdentifierId must be a valid GUID.");

            identifier.RuleFor(i => i.Type)
                .IsInEnum()
                .WithMessage("Type must be a valid IdentifierType.");

            identifier.RuleFor(i => i.Value)
                .NotEmpty()
                .WithMessage("Value is required.");
        });
    }

    private static int CountSubOperations(CreatePartyComposite command)
        => 1 + command.ContactChannels.Count + command.Identifiers.Count;
}
