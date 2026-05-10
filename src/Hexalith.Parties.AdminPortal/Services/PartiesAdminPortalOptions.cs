namespace Hexalith.Parties.AdminPortal.Services;

public sealed class PartiesAdminPortalOptions
{
    [Obsolete("Admin portal reads now flow through FrontComposer IQueryService/EventStore. Configure EventStore at the shell boundary instead.")]
    public Uri? ApiBaseAddress { get; set; }

    public Uri? EventStoreAdminUiBaseAddress { get; set; }

    public string EventStoreAdminUiStreamPath { get; set; } = "streams";

    public string EventStoreAdminUiCorrelationPath { get; set; } = "correlations";

    public string Domain { get; set; } = "party";

    public string? ListProjectionType { get; set; }

    public string? ListQueryType { get; set; }

    public string? SearchProjectionType { get; set; }

    public string? SearchQueryType { get; set; }

    public string? DetailProjectionType { get; set; }

    public string? DetailQueryType { get; set; }

    public string? DetailProjectionActorType { get; set; }
}
