using Microsoft.Extensions.Options;

namespace Hexalith.Parties.AdminPortal.Services;

public sealed class AdminPortalEventStoreAdminLinks(IOptions<PartiesAdminPortalOptions> options)
{
    private readonly PartiesAdminPortalOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    public Uri? BuildStreamLink(string aggregateId)
        => BuildLink(_options.EventStoreAdminUiStreamPath, "aggregateId", aggregateId);

    public Uri? BuildCorrelationLink(string correlationId)
        => BuildLink(_options.EventStoreAdminUiCorrelationPath, "correlationId", correlationId);

    private Uri? BuildLink(string path, string parameterName, string value)
    {
        if (_options.EventStoreAdminUiBaseAddress is null || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        UriBuilder builder = new(_options.EventStoreAdminUiBaseAddress)
        {
            Path = CombinePath(_options.EventStoreAdminUiBaseAddress.AbsolutePath, path),
            Query = $"{parameterName}={Uri.EscapeDataString(value)}",
        };

        return builder.Uri;
    }

    private static string CombinePath(string basePath, string relativePath)
    {
        string left = string.IsNullOrWhiteSpace(basePath) ? string.Empty : basePath.TrimEnd('/');
        string right = string.IsNullOrWhiteSpace(relativePath) ? string.Empty : relativePath.TrimStart('/');
        return string.IsNullOrEmpty(left)
            ? right
            : string.IsNullOrEmpty(right) ? left : $"{left}/{right}";
    }
}
