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

    public static string ConsentLoadingStatus => Get(nameof(ConsentLoadingStatus));

    public static string ConsentLoadingHeading => Get(nameof(ConsentLoadingHeading));

    public static string ConsentFailureTitle => Get(nameof(ConsentFailureTitle));

    public static string ConsentFailureMessage => Get(nameof(ConsentFailureMessage));

    public static string ConsentDeletedTitle => Get(nameof(ConsentDeletedTitle));

    public static string ConsentDeletedMessage => Get(nameof(ConsentDeletedMessage));

    public static string ConsentControlledTitle => Get(nameof(ConsentControlledTitle));

    public static string ConsentKeptTitle => Get(nameof(ConsentKeptTitle));

    public static string ConsentMarketingEmailsTitle => Get(nameof(ConsentMarketingEmailsTitle));

    public static string ConsentMarketingEmailsDescription => Get(nameof(ConsentMarketingEmailsDescription));

    public static string ConsentProductUpdatesTitle => Get(nameof(ConsentProductUpdatesTitle));

    public static string ConsentProductUpdatesDescription => Get(nameof(ConsentProductUpdatesDescription));

    public static string ConsentBasisConsent => Get(nameof(ConsentBasisConsent));

    public static string ConsentBasisContract => Get(nameof(ConsentBasisContract));

    public static string ConsentBasisLegal => Get(nameof(ConsentBasisLegal));

    public static string ConsentBasisLegitimateInterest => Get(nameof(ConsentBasisLegitimateInterest));

    public static string ConsentAccountServiceTitle => Get(nameof(ConsentAccountServiceTitle));

    public static string ConsentAccountServiceDescription => Get(nameof(ConsentAccountServiceDescription));

    public static string ConsentLegalRecordsTitle => Get(nameof(ConsentLegalRecordsTitle));

    public static string ConsentLegalRecordsDescription => Get(nameof(ConsentLegalRecordsDescription));

    public static string ConsentLegitimateInterestTitle => Get(nameof(ConsentLegitimateInterestTitle));

    public static string ConsentLegitimateInterestDescription => Get(nameof(ConsentLegitimateInterestDescription));

    public static string ConsentObjectAction => Get(nameof(ConsentObjectAction));

    public static string ConsentNoChannel => Get(nameof(ConsentNoChannel));

    public static string ConsentPendingConfirmation => Get(nameof(ConsentPendingConfirmation));

    public static string ConsentSaving => Get(nameof(ConsentSaving));

    public static string ConsentSavedUpdating => Get(nameof(ConsentSavedUpdating));

    public static string ConsentSaved => Get(nameof(ConsentSaved));

    public static string ConsentSavedLatestAvailable => Get(nameof(ConsentSavedLatestAvailable));

    public static string ConsentSaveFailure => Get(nameof(ConsentSaveFailure));

    public static string PrivacyTitle => Get(nameof(PrivacyTitle));

    public static string PrivacySummary => Get(nameof(PrivacySummary));

    public static string PrivacyStatus => Get(nameof(PrivacyStatus));

    public static string PrivacyNextExport => Get(nameof(PrivacyNextExport));

    public static string PrivacyNextErasure => Get(nameof(PrivacyNextErasure));

    public static string PrivacyExportTitle => Get(nameof(PrivacyExportTitle));

    public static string PrivacyExportFormat => Get(nameof(PrivacyExportFormat));

    public static string PrivacyExportDescription => Get(nameof(PrivacyExportDescription));

    public static string PrivacyExportIdleStatus => Get(nameof(PrivacyExportIdleStatus));

    public static string PrivacyExportAction => Get(nameof(PrivacyExportAction));

    public static string PrivacyExportPreparing => Get(nameof(PrivacyExportPreparing));

    public static string PrivacyExportReady => Get(nameof(PrivacyExportReady));

    public static string PrivacyExportRestrictedReady => Get(nameof(PrivacyExportRestrictedReady));

    public static string PrivacyExportErasedReady => Get(nameof(PrivacyExportErasedReady));

    public static string PrivacyExportUnavailableReady => Get(nameof(PrivacyExportUnavailableReady));

    public static string PrivacyExportDownload => Get(nameof(PrivacyExportDownload));

    public static string PrivacyExportFailureTitle => Get(nameof(PrivacyExportFailureTitle));

    public static string PrivacyExportFailureMessage => Get(nameof(PrivacyExportFailureMessage));

    public static string PrivacyErasureTitle => Get(nameof(PrivacyErasureTitle));

    public static string PrivacyErasureDescription => Get(nameof(PrivacyErasureDescription));

    public static string PrivacyErasureIdleStatus => Get(nameof(PrivacyErasureIdleStatus));

    public static string PrivacyErasureLoadingStatus => Get(nameof(PrivacyErasureLoadingStatus));

    public static string PrivacyErasureAction => Get(nameof(PrivacyErasureAction));

    public static string PrivacyErasureConfirmTitle => Get(nameof(PrivacyErasureConfirmTitle));

    public static string PrivacyErasureConfirmMessage => Get(nameof(PrivacyErasureConfirmMessage));

    public static string PrivacyErasureConfirmAction => Get(nameof(PrivacyErasureConfirmAction));

    public static string PrivacyErasureKeepData => Get(nameof(PrivacyErasureKeepData));

    public static string PrivacyErasureRequesting => Get(nameof(PrivacyErasureRequesting));

    public static string PrivacyErasurePendingCancellable => Get(nameof(PrivacyErasurePendingCancellable));

    public static string PrivacyErasureCancelAction => Get(nameof(PrivacyErasureCancelAction));

    public static string PrivacyErasureCancelling => Get(nameof(PrivacyErasureCancelling));

    public static string PrivacyErasureCancelled => Get(nameof(PrivacyErasureCancelled));

    public static string PrivacyErasureStarted => Get(nameof(PrivacyErasureStarted));

    public static string PrivacyErasureCancelUnavailable => Get(nameof(PrivacyErasureCancelUnavailable));

    public static string PrivacyErasurePermanent => Get(nameof(PrivacyErasurePermanent));

    public static string PrivacyErasureFailureTitle => Get(nameof(PrivacyErasureFailureTitle));

    public static string PrivacyErasureFailureMessage => Get(nameof(PrivacyErasureFailureMessage));

    public static string PrivacyErasureRejectedMessage => Get(nameof(PrivacyErasureRejectedMessage));

    public static string PrivacyErasureUnavailableMessage => Get(nameof(PrivacyErasureUnavailableMessage));

    public static string PrivacyErasureForbiddenMessage => Get(nameof(PrivacyErasureForbiddenMessage));

    public static string PrivacyProcessingTitle => Get(nameof(PrivacyProcessingTitle));

    public static string PrivacyProcessingDescription => Get(nameof(PrivacyProcessingDescription));

    public static string PrivacyProcessingLoadingStatus => Get(nameof(PrivacyProcessingLoadingStatus));

    public static string PrivacyProcessingReadyStatus => Get(nameof(PrivacyProcessingReadyStatus));

    public static string PrivacyProcessingEmptyStatus => Get(nameof(PrivacyProcessingEmptyStatus));

    public static string PrivacyProcessingFailureTitle => Get(nameof(PrivacyProcessingFailureTitle));

    public static string PrivacyProcessingFailureMessage => Get(nameof(PrivacyProcessingFailureMessage));

    public static string PrivacyProcessingForbiddenMessage => Get(nameof(PrivacyProcessingForbiddenMessage));

    public static string PrivacyProcessingUnavailableMessage => Get(nameof(PrivacyProcessingUnavailableMessage));

    public static string PrivacyProcessingStaleMessage => Get(nameof(PrivacyProcessingStaleMessage));

    public static string PrivacyProcessingErasedMessage => Get(nameof(PrivacyProcessingErasedMessage));

    public static string PrivacyProcessingSummaryFallback => Get(nameof(PrivacyProcessingSummaryFallback));

    public static string PrivacyProcessingConsentSummary => Get(nameof(PrivacyProcessingConsentSummary));

    public static string PrivacyProcessingManageConsent => Get(nameof(PrivacyProcessingManageConsent));

    public static string PrivacyProcessingBoundedMetadata => Get(nameof(PrivacyProcessingBoundedMetadata));

    public static string PrivacyProcessingTimestampFormat => Get(nameof(PrivacyProcessingTimestampFormat));

    public static string ContactChannelTypeLabel(ContactChannelType type)
        => Get($"{nameof(ContactChannelTypeLabel)}{type}");

    public static string IdentifierTypeLabel(IdentifierType type)
        => Get($"{nameof(IdentifierTypeLabel)}{type}");

    public static string PartyTypeLabel(PartyType type)
        => Get($"{nameof(PartyTypeLabel)}{type}");

    public static string PrivacyProcessingCategoryLabel(ConsumerPrivacyProcessingCategory category)
        => Get($"{nameof(PrivacyProcessingCategoryLabel)}{category}");

    public static string PrivacyProcessingOutcomeLabel(ConsumerPrivacyProcessingRecordOutcome outcome)
        => Get($"{nameof(PrivacyProcessingOutcomeLabel)}{outcome}");

    public static string FormatPrivacyProcessingTimestamp(DateTimeOffset timestamp)
        => string.Format(
            CultureInfo.CurrentCulture,
            PrivacyProcessingTimestampFormat,
            timestamp.ToLocalTime());

    private static string Get(string name)
        => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
