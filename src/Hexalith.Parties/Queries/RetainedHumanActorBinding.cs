using Hexalith.Parties.Contracts.Models;

namespace Hexalith.Parties.Queries;

/// <summary>A retained interval and the immutable source position that opened it.</summary>
/// <param name="Evidence">The recorded actor and half-open interval.</param>
/// <param name="SourcePosition">The original establishment or rebind event position.</param>
internal sealed record RetainedHumanActorBinding(HumanActorBindingEvidence Evidence, long SourcePosition);
