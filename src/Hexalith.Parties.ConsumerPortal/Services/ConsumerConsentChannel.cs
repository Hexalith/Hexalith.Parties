using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.ConsumerPortal.Services;

public sealed record ConsumerConsentChannel(string ChannelId, ContactChannelType Type);
