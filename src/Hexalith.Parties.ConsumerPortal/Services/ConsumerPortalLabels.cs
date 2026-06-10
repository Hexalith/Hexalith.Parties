using System.Globalization;
using System.Resources;

namespace Hexalith.Parties.ConsumerPortal.Services;

public static class ConsumerPortalLabels
{
    private static readonly ResourceManager Resources = new(
        "Hexalith.Parties.ConsumerPortal.Resources.ConsumerPortalResources",
        typeof(ConsumerPortalLabels).Assembly);

    public static string AreaLabel => Get(nameof(AreaLabel));

    public static string NextStepsTitle => Get(nameof(NextStepsTitle));

    public static string MyProfileTitle => Get(nameof(MyProfileTitle));

    public static string MyProfileSummary => Get(nameof(MyProfileSummary));

    public static string MyProfileStatus => Get(nameof(MyProfileStatus));

    public static string MyProfileNextIdentity => Get(nameof(MyProfileNextIdentity));

    public static string MyProfileNextData => Get(nameof(MyProfileNextData));

    public static string EditProfileTitle => Get(nameof(EditProfileTitle));

    public static string EditProfileSummary => Get(nameof(EditProfileSummary));

    public static string EditProfileStatus => Get(nameof(EditProfileStatus));

    public static string EditProfileNextReview => Get(nameof(EditProfileNextReview));

    public static string EditProfileNextSubmit => Get(nameof(EditProfileNextSubmit));

    public static string ConsentTitle => Get(nameof(ConsentTitle));

    public static string ConsentSummary => Get(nameof(ConsentSummary));

    public static string ConsentStatus => Get(nameof(ConsentStatus));

    public static string ConsentNextChoices => Get(nameof(ConsentNextChoices));

    public static string ConsentNextRecords => Get(nameof(ConsentNextRecords));

    public static string PrivacyTitle => Get(nameof(PrivacyTitle));

    public static string PrivacySummary => Get(nameof(PrivacySummary));

    public static string PrivacyStatus => Get(nameof(PrivacyStatus));

    public static string PrivacyNextExport => Get(nameof(PrivacyNextExport));

    public static string PrivacyNextErasure => Get(nameof(PrivacyNextErasure));

    private static string Get(string name)
        => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
