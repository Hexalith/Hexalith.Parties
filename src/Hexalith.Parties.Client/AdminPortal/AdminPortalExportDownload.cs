namespace Hexalith.Parties.Client.AdminPortal;

public sealed record AdminPortalExportDownload(
    string FileName,
    string ContentType,
    byte[] Payload);
