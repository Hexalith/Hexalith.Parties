using Hexalith.Parties.Contracts.Security;

namespace Hexalith.Parties.ConsumerPortal.Services;

public sealed record ConsumerConsentGrantRequest(string ChannelId, string Purpose, LawfulBasis LawfulBasis);
