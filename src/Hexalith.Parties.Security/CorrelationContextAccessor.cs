using System.Threading;

namespace Hexalith.Parties.Security;

public sealed class CorrelationContextAccessor : ICorrelationContextAccessor
{
    private static readonly AsyncLocal<string?> s_correlationId = new();

    public string? CorrelationId
    {
        get => s_correlationId.Value;
        set => s_correlationId.Value = value;
    }
}