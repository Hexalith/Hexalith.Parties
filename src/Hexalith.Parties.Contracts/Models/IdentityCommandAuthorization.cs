using Hexalith.EventStore.Contracts.Security;
namespace Hexalith.Parties.Contracts.Models;

/// <summary>Processor-owned verified context, excluded from public command JSON.</summary>
/// <param name="Admission">The cryptographically verified exact-operation evidence.</param>
/// <param name="SourcePosition">The actual command processing source checkpoint.</param>
/// <param name="Policy">The explicitly configured history policy.</param>
/// <param name="Custody">The enforceable provider admission, required for binding writes.</param>
public sealed record IdentityCommandAuthorization(
    IdentityAdmissionEvidence Admission,
    long SourcePosition,
    IdentityHistoryPolicy? Policy,
    IdentityHistoryCustodyEvidence? Custody);
