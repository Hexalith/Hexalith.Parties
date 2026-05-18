namespace Hexalith.Parties.Client.Abstractions;

public sealed record PartiesCommandResult<TPayload>(string CorrelationId, TPayload? Payload)
    where TPayload : class;
