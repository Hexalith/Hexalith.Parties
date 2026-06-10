using System.Globalization;
using System.Resources;

using Hexalith.Parties.Contracts.ValueObjects;

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

    public static string MyProfileLoadingStatus => Get(nameof(MyProfileLoadingStatus));

    public static string MyProfileLoadingHeading => Get(nameof(MyProfileLoadingHeading));

    public static string MyProfileFailureTitle => Get(nameof(MyProfileFailureTitle));

    public static string MyProfileFailureMessage => Get(nameof(MyProfileFailureMessage));

    public static string MyProfileRetry => Get(nameof(MyProfileRetry));

    public static string MyProfileDeletedTitle => Get(nameof(MyProfileDeletedTitle));

    public static string MyProfileDeletedMessage => Get(nameof(MyProfileDeletedMessage));

    public static string MyProfileFreshnessCurrent => Get(nameof(MyProfileFreshnessCurrent));

    public static string MyProfileFreshnessStale => Get(nameof(MyProfileFreshnessStale));

    public static string MyProfileFreshnessLastKnown => Get(nameof(MyProfileFreshnessLastKnown));

    public static string MyProfileDetailsTitle => Get(nameof(MyProfileDetailsTitle));

    public static string MyProfilePersonTitle => Get(nameof(MyProfilePersonTitle));

    public static string MyProfileOrganizationTitle => Get(nameof(MyProfileOrganizationTitle));

    public static string MyProfileContactTitle => Get(nameof(MyProfileContactTitle));

    public static string MyProfileIdentifiersTitle => Get(nameof(MyProfileIdentifiersTitle));

    public static string MyProfileStateTitle => Get(nameof(MyProfileStateTitle));

    public static string MyProfileDatesTitle => Get(nameof(MyProfileDatesTitle));

    public static string MyProfileDisplayName => Get(nameof(MyProfileDisplayName));

    public static string MyProfilePartyType => Get(nameof(MyProfilePartyType));

    public static string MyProfileFirstName => Get(nameof(MyProfileFirstName));

    public static string MyProfileLastName => Get(nameof(MyProfileLastName));

    public static string MyProfileDateOfBirth => Get(nameof(MyProfileDateOfBirth));

    public static string MyProfilePrefix => Get(nameof(MyProfilePrefix));

    public static string MyProfileSuffix => Get(nameof(MyProfileSuffix));

    public static string MyProfileLegalName => Get(nameof(MyProfileLegalName));

    public static string MyProfileTradingName => Get(nameof(MyProfileTradingName));

    public static string MyProfileLegalForm => Get(nameof(MyProfileLegalForm));

    public static string MyProfileRegistrationNumber => Get(nameof(MyProfileRegistrationNumber));

    public static string MyProfileNaturalPerson => Get(nameof(MyProfileNaturalPerson));

    public static string MyProfileContactPreferred => Get(nameof(MyProfileContactPreferred));

    public static string MyProfileIdentifierJurisdiction => Get(nameof(MyProfileIdentifierJurisdiction));

    public static string MyProfileLifecycleState => Get(nameof(MyProfileLifecycleState));

    public static string MyProfileRestrictionState => Get(nameof(MyProfileRestrictionState));

    public static string MyProfileCreatedAt => Get(nameof(MyProfileCreatedAt));

    public static string MyProfileLastModifiedAt => Get(nameof(MyProfileLastModifiedAt));

    public static string MyProfileEmptyContacts => Get(nameof(MyProfileEmptyContacts));

    public static string MyProfileEmptyIdentifiers => Get(nameof(MyProfileEmptyIdentifiers));

    public static string MyProfileActive => Get(nameof(MyProfileActive));

    public static string MyProfileInactive => Get(nameof(MyProfileInactive));

    public static string MyProfileRestricted => Get(nameof(MyProfileRestricted));

    public static string MyProfileNotRestricted => Get(nameof(MyProfileNotRestricted));

    public static string MyProfileYes => Get(nameof(MyProfileYes));

    public static string MyProfileNo => Get(nameof(MyProfileNo));

    public static string MyProfileNotProvided => Get(nameof(MyProfileNotProvided));

    public static string EditProfileTitle => Get(nameof(EditProfileTitle));

    public static string EditProfileSummary => Get(nameof(EditProfileSummary));

    public static string EditProfileStatus => Get(nameof(EditProfileStatus));

    public static string EditProfileNextReview => Get(nameof(EditProfileNextReview));

    public static string EditProfileNextSubmit => Get(nameof(EditProfileNextSubmit));

    public static string EditProfileLoadingStatus => Get(nameof(EditProfileLoadingStatus));

    public static string EditProfileLoadingHeading => Get(nameof(EditProfileLoadingHeading));

    public static string EditProfileFailureTitle => Get(nameof(EditProfileFailureTitle));

    public static string EditProfileFailureMessage => Get(nameof(EditProfileFailureMessage));

    public static string EditProfileRetry => Get(nameof(EditProfileRetry));

    public static string EditProfileDeletedTitle => Get(nameof(EditProfileDeletedTitle));

    public static string EditProfileDeletedMessage => Get(nameof(EditProfileDeletedMessage));

    public static string EditProfileDetailsTitle => Get(nameof(EditProfileDetailsTitle));

    public static string EditProfileEditableFieldsTitle => Get(nameof(EditProfileEditableFieldsTitle));

    public static string EditProfileReadOnlyContactTitle => Get(nameof(EditProfileReadOnlyContactTitle));

    public static string EditProfileReadOnlyIdentifierTitle => Get(nameof(EditProfileReadOnlyIdentifierTitle));

    public static string EditProfileReadOnlyHelp => Get(nameof(EditProfileReadOnlyHelp));

    public static string EditProfileSave => Get(nameof(EditProfileSave));

    public static string EditProfileCancel => Get(nameof(EditProfileCancel));

    public static string EditProfileSaving => Get(nameof(EditProfileSaving));

    public static string EditProfileSavedUpdating => Get(nameof(EditProfileSavedUpdating));

    public static string EditProfileSavedCurrent => Get(nameof(EditProfileSavedCurrent));

    public static string EditProfileDegradedAfterSave => Get(nameof(EditProfileDegradedAfterSave));

    public static string EditProfileValidationRetry => Get(nameof(EditProfileValidationRetry));

    public static string EditProfileSaveFailure => Get(nameof(EditProfileSaveFailure));

    public static string EditProfileValidationRequired => Get(nameof(EditProfileValidationRequired));

    public static string EditProfileValidationDate => Get(nameof(EditProfileValidationDate));

    public static string EditProfileFieldHint => Get(nameof(EditProfileFieldHint));

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

    public static string ContactChannelTypeLabel(ContactChannelType type)
        => Get($"{nameof(ContactChannelTypeLabel)}{type}");

    public static string IdentifierTypeLabel(IdentifierType type)
        => Get($"{nameof(IdentifierTypeLabel)}{type}");

    public static string PartyTypeLabel(PartyType type)
        => Get($"{nameof(PartyTypeLabel)}{type}");

    private static string Get(string name)
        => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
