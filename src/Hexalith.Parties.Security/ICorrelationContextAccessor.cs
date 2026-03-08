namespace Hexalith.Parties.Security;

/// <summary>
/// Provides ambient access to the current correlation identifier across async flows.
/// </summary>
public interface ICorrelationContextAccessor
{
    string? CorrelationId { get; set; }
}