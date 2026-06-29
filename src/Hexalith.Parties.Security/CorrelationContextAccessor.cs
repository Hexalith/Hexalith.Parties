namespace Hexalith.Parties.Security;

public sealed class CorrelationContextAccessor : ICorrelationContextAccessor
{
    private readonly Hexalith.Commons.Http.CorrelationContextAccessor _inner = new();

    public string? CorrelationId
    {
        get => _inner.CorrelationId;
        set => _inner.CorrelationId = value;
    }
}
