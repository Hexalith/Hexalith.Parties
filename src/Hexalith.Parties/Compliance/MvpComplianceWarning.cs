namespace Hexalith.Parties.Compliance;

public static class MvpComplianceWarning
{
    public const string ActivationConfigurationKey = "Parties:Compliance:GdprFeaturesActive";

    public const string HeaderName = "X-Hexalith-Parties-Mvp-Compliance-Warning";

    public const string Message =
        "Hexalith.Parties MVP is not for regulated EU personal data until v1.1 GDPR features are active.";
}
