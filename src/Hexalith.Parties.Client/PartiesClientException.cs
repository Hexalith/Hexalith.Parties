namespace Hexalith.Parties.Client;

public sealed class PartiesClientException : Exception
{
    public PartiesClientException(int status, string title, string? type, string? detail, string? correlationId)
        : base(detail ?? title)
    {
        Status = status;
        Title = title;
        Type = type;
        Detail = detail;
        CorrelationId = correlationId;
    }

    public PartiesClientException()
        : base()
    {
    }

    public PartiesClientException(string message)
        : base(message)
    {
    }

    public PartiesClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public int Status { get; }

    public string? Title { get; }

    public string? Type { get; }

    public string? Detail { get; }

    public string? CorrelationId { get; }
}
