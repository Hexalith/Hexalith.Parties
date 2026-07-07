using FluentValidation;

using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Server.Aggregates;

using SemanticId = Hexalith.Parties.Contracts.ValueObjects.PartyIdentifier;

namespace Hexalith.Parties.Validation;

public sealed class UpdatePartyCompositeValidator : AbstractValidator<UpdatePartyComposite>
{
    public UpdatePartyCompositeValidator()
    {
        RuleFor(x => x.PartyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("PartyId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("PartyId must be a support-safe identifier.");

        RuleFor(x => x)
            .Must(x => CountSubOperations(x) <= PartyAggregate.GetEffectiveMaxSubOperations())
            .WithMessage(x => $"Total sub-operations ({CountSubOperations(x)}) exceeds maximum ({PartyAggregate.GetEffectiveMaxSubOperations()}).");

        RuleFor(x => x)
            .Must(HaveMatchingChildPartyIds)
            .WithMessage("Child PartyId must match PartyId.");

        RuleForEach(x => x.AddContactChannels).ChildRules(channel =>
        {
            channel.RuleFor(c => c.PartyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("PartyId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("PartyId must be a support-safe identifier.");

            channel.RuleFor(c => c.ContactChannelId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("ContactChannelId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("ContactChannelId must be a support-safe identifier.");

            channel.RuleFor(c => c.Type)
                .IsInEnum()
                .WithMessage("Type must be a valid ContactChannelType.");

            channel.RuleFor(c => c.Value)
                .NotEmpty()
                .WithMessage("Value is required.");
        });

        RuleForEach(x => x.UpdateContactChannels).ChildRules(channel =>
        {
            channel.RuleFor(c => c.PartyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("PartyId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("PartyId must be a support-safe identifier.");

            channel.RuleFor(c => c.ContactChannelId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("ContactChannelId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("ContactChannelId must be a support-safe identifier.");
        });

        RuleForEach(x => x.RemoveContactChannelIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("RemoveContactChannelId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("RemoveContactChannelId must be a support-safe identifier.");

        RuleForEach(x => x.AddIdentifiers).ChildRules(identifier =>
        {
            identifier.RuleFor(i => i.PartyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("PartyId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("PartyId must be a support-safe identifier.");

            identifier.RuleFor(i => i.IdentifierId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("IdentifierId is required.")
                .Must(SemanticId.IsValid)
                .WithMessage("IdentifierId must be a support-safe identifier.");

            identifier.RuleFor(i => i.Type)
                .IsInEnum()
                .WithMessage("Type must be a valid IdentifierType.");

            identifier.RuleFor(i => i.Value)
                .NotEmpty()
                .WithMessage("Value is required.");
        });

        RuleForEach(x => x.RemoveIdentifierIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("RemoveIdentifierId is required.")
            .Must(SemanticId.IsValid)
            .WithMessage("RemoveIdentifierId must be a support-safe identifier.");
    }

    private static int CountSubOperations(UpdatePartyComposite command)
        => (command.PersonDetails is not null ? 1 : 0)
            + (command.OrganizationDetails is not null ? 1 : 0)
            + command.AddContactChannels.Count
            + command.UpdateContactChannels.Count
            + command.RemoveContactChannelIds.Count
            + command.AddIdentifiers.Count
            + command.RemoveIdentifierIds.Count;

    private static bool HaveMatchingChildPartyIds(UpdatePartyComposite command)
        => command.AddContactChannels.All(channel => channel is not null && string.Equals(channel.PartyId, command.PartyId, StringComparison.Ordinal))
            && command.UpdateContactChannels.All(channel => channel is not null && string.Equals(channel.PartyId, command.PartyId, StringComparison.Ordinal))
            && command.AddIdentifiers.All(identifier => identifier is not null && string.Equals(identifier.PartyId, command.PartyId, StringComparison.Ordinal));
}
