namespace Hexalith.Parties.Projections.Handlers;

/// <summary>Signals a bounded, retryable projection delivery validation failure.</summary>
internal sealed class PartyProjectionDeliveryException : Exception
{
    /// <summary>Initializes a new instance with a non-sensitive reason code.</summary>
    /// <param name="reason">The bounded delivery failure reason.</param>
    internal PartyProjectionDeliveryException(string reason)
        : base(reason)
    {
    }
}
