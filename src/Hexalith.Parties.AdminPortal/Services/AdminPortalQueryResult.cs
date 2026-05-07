namespace Hexalith.Parties.AdminPortal.Services;

public sealed record AdminPortalQueryResult<T>(T Payload, AdminPortalQueryMetadata Metadata);
