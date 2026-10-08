import { expect, test } from '@playwright/test';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const SPEC_DIR = dirname(fileURLToPath(import.meta.url));
const REPOSITORY_ROOT = resolve(SPEC_DIR, '../../..');
const STORY_PATH = '_bmad-output/implementation-artifacts/7-4-projection-platform-compatibility-adapter.md';
const MIGRATION_STORY_PATH = '_bmad-output/implementation-artifacts/8-6-projection-and-query-sdk-migration.md';
const SERVICE_REGISTRATION_PATH = 'src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs';
const DETAIL_HANDLER_PATH = 'src/Hexalith.Parties.Projections/Handlers/PartyDetailSdkProjectionHandler.cs';
const INDEX_HANDLER_PATH = 'src/Hexalith.Parties.Projections/Handlers/PartyIndexSdkProjectionHandler.cs';
const FOLD_PATH = 'src/Hexalith.Parties.Projections/Handlers/PartySdkProjectionFold.cs';
const QUERY_SERVICE_PATH = 'src/Hexalith.Parties/Queries/PartySdkQueryService.cs';
const REGISTRATION_TESTS_PATH = 'tests/Hexalith.Parties.Tests/Projections/ProjectionPlatformAdapterTests.cs';
const HANDLER_TESTS_PATH = 'tests/Hexalith.Parties.Projections.Tests/Handlers/PartySdkProjectionHandlerTests.cs';
const QUERY_TESTS_PATH = 'tests/Hexalith.Parties.Tests/Gateway/PartySdkQueryHandlerTests.cs';
const RETIRED_PROJECTION_PATHS = [
  'src/Hexalith.Parties.Projections/Abstractions/IPartyDetailProjectionActor.cs',
  'src/Hexalith.Parties.Projections/Abstractions/IPartyIndexProjectionActor.cs',
  'src/Hexalith.Parties.Projections/Actors/PartyDetailProjectionActor.cs',
  'src/Hexalith.Parties.Projections/Actors/PartyIndexProjectionActor.cs',
  'src/Hexalith.Parties.Projections/Configuration/PartyProjectionPlatformAdapterMode.cs',
  'src/Hexalith.Parties.Projections/Configuration/ProjectionOptions.cs',
  'src/Hexalith.Parties.Projections/Services/IPartyProjectionPlatformAdapter.cs',
  'src/Hexalith.Parties.Projections/Services/IProjectionRebuildService.cs',
  'src/Hexalith.Parties.Projections/Services/LocalPartyProjectionPlatformAdapter.cs',
  'src/Hexalith.Parties.Projections/Services/PartyProjectionPlatformFreshness.cs',
  'src/Hexalith.Parties.Projections/Services/PartyProjectionRebuildCheckpoint.cs',
  'src/Hexalith.Parties.Projections/Services/PartyProjectionRebuildScope.cs',
  'src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs',
  'src/Hexalith.Parties/Domain/EventStorePartyProjectionPlatformAdapter.cs',
  'src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs',
  'src/Hexalith.Parties/Extensions/PartyDetailProjectionActorExtensions.cs',
  'src/Hexalith.Parties/HealthChecks/ProjectionActorsHealthCheck.cs',
  'src/Hexalith.Parties/Queries/IPartyProjectionQueryActor.cs',
];

test.describe('Story 7.4 projection platform compatibility adapter', () => {
  test('documents adapter-first parity, rollback, and blocked validation evidence', () => {
    const story = readRepositoryFile(STORY_PATH);

    expect(story).toContain('Status: done');
    expect(story).toContain('Parties:Projections:PlatformAdapterMode');
    expect(story).toContain('Default remains `EventStore`');
    expect(story).toContain('Story 7.5 remains responsible for deleting local checkpoint/rebuild infrastructure');
    expect(story).toContain('No public Parties read contracts');
    expect(story).toContain('EventStore submodule source was not modified');
    expect(story).toContain('Hexalith.Commons.UniqueIds >= 3.19.0');
  });

  test('keeps the approved SDK-only boundary and retires every rollback projection path', () => {
    const migration = readRepositoryFile(MIGRATION_STORY_PATH);
    const registrations = readRepositoryFile(SERVICE_REGISTRATION_PATH);
    const detail = readRepositoryFile(DETAIL_HANDLER_PATH);
    const index = readRepositoryFile(INDEX_HANDLER_PATH);
    const queries = readRepositoryFile(QUERY_SERVICE_PATH);

    expect(migration).toContain('Administrator chose option 1');
    expect(migration).toContain('SDK-only verification matrix');
    for (const path of RETIRED_PROJECTION_PATHS) {
      expect(existsSync(resolve(REPOSITORY_ROOT, path)), `${path} remains retired by Story 8.6`).toBe(false);
    }
    expect(detail).toContain('IAsyncDomainProjectionRebuildHandler');
    expect(index).toContain('IAsyncDomainSharedProjectionRebuildCompletionHandler');
    expect(registrations).toContain('AddOptions<PartySdkReadModelOptions>');
    expect(registrations).toContain('AddScoped<PartySdkQueryService>');
    expect(queries).toContain('IReadModelStore readModelStore');
    expect(queries).toContain('ProjectionFreshnessStatus.Current');
    expect(queries).toContain('ProjectionFreshnessStatus.Unavailable');
  });

  test('preserves ordered replay, duplicate and gap checks, and coordinated checkpoint persistence', () => {
    const fold = readRepositoryFile(FOLD_PATH);
    const detail = readRepositoryFile(DETAIL_HANDLER_PATH);
    const index = readRepositoryFile(INDEX_HANDLER_PATH);

    expect(fold).toContain('events.OrderBy(static item => item.SequenceNumber)');
    expect(fold).toContain('delivery-sequence-gap');
    expect(fold).toContain('conflicting-duplicate-event');
    expect(detail).toContain('currentProcessing.Value?.LastSequenceNumber');
    expect(detail).toContain('nextProcessing.LastSequenceNumber == currentProcessing.Value.LastSequenceNumber');
    expect(detail).toContain('ReadModelBatchOperation.Write(key, next, concurrency)');
    expect(detail).toContain('ReadModelBatchOperation.Write(processingKey, nextProcessing, processingConcurrency)');
    const failureGuardIndex = detail.indexOf('GetDeliveryFailureReason(request.Events, oldestCheckpoint)');
    const batchExecuteIndex = detail.indexOf('batchStore.ExecuteAsync(batch, cancellationToken)');
    expect(failureGuardIndex).toBeGreaterThanOrEqual(0);
    expect(batchExecuteIndex).toBeGreaterThan(failureGuardIndex);
    expect(index).toContain('GetDeliveryFailureReason(request.Events, lastSequence)');
    expect(index).toContain('DomainProjectionHandlerResult.AlreadyCompleted()');
    expect(index).toContain('ReadModelWritePolicy.UpdateAsync<PartyIndexSdkReadModel>');
  });

  test('uses full replay and snapshot concurrency for canonical SDK rebuild plans', () => {
    const detail = readRepositoryFile(DETAIL_HANDLER_PATH);
    const index = readRepositoryFile(INDEX_HANDLER_PATH);
    const fold = readRepositoryFile(FOLD_PATH);
    const queries = readRepositoryFile(QUERY_SERVICE_PATH);

    expect(detail).toContain('DomainProjectionRebuildSemantics.FullReplay');
    expect(detail).toContain('GetDeliveryFailureReason(request.Events, long.MinValue)');
    expect(detail).toContain('Fold(request, current: null)');
    expect(detail).toContain('ReadModelBatchConcurrency.Match(etag)');
    expect(detail).toContain('ReadModelBatchConcurrency.Match(processingEtag)');
    expect(index).toContain('CreateEmptyCandidateAsync');
    expect(index).toContain('AccumulateAsync');
    expect(index).toContain('FinalizeAsync');
    expect(index).toContain('PartySdkReadModelAddresses.Index(identity.TenantId)');
    expect(index).toContain('ReadModelBatchConcurrency.Match(etag)');
    expect(fold).toContain('PartyEventTypeResolver.Resolve');
    expect(fold).not.toContain('Type.GetType');
    expect(queries).toContain('GetProcessingRecordsAsync');
    expect(queries).toContain('Party = detail.IsErased || unavailable ? null : detail');
  });

  test('pins SDK delivery, rebuild, degraded-read, and GDPR coverage after the approved migration', () => {
    const registrations = readRepositoryFile(REGISTRATION_TESTS_PATH);
    const handlers = readRepositoryFile(HANDLER_TESTS_PATH);
    const queries = readRepositoryFile(QUERY_TESTS_PATH);

    expect(registrations).toContain('AddParties_UsesSdkReadModelsAndCursorCodecWithoutLocalProjectionMechanics');
    expect(registrations).toContain('AddParties_ErasureCleanupDelegatesInvokeRealEraserAndCacheEvictionWithCorrectKeysAsync');
    for (const testName of [
      'DetailHandler_WritesCanonicalBatchAndMatchesRetainedFoldAsync',
      'DetailHandler_DuplicateDeliveryReturnsAlreadyCompletedAsync',
      'DetailHandler_ConflictingDuplicateSequenceIsRetryableWithoutWriteAsync',
      'DetailHandler_SeparateDeliveryGapIsRetryableWithoutCoordinatedWriteAsync',
      'IndexHandler_DuplicateDeliveryReturnsAlreadyCompletedAsync',
      'IndexHandler_ConcurrencyRetryRefoldsAndPreservesUnrelatedEntryAsync',
      'IndexHandler_SeparateDeliveryGapIsRetryableWithoutPersistenceAsync',
      'DetailRebuildPlan_MatchesNormalReplayAfterTimestampNormalizationAsync',
      'DetailRebuildPlan_ExistingSlotsRequireSnapshotEtagsAsync',
      'DetailRebuildPlan_UnresolvedEventFailsWithoutProducingCandidateAsync',
      'SharedIndexRebuild_AccumulatesCompleteHistoriesIntoOneReplacementAsync',
      'SharedIndexRebuild_ExistingIndexRequiresSnapshotEtagAsync',
    ]) {
      expect(handlers).toContain(testName);
    }
    expect(queries).toContain('DetailHandler_StateStoreFailureReturnsTenantScopedLastKnownDataAsStaleAsync');
    expect(queries).toContain('GdprHandlers_PreserveExportProcessingStatusAndCertificateSemanticsAsync');
    expect(queries).toContain('DetailHandlers_ErasedPartyReturnOnlyRedactedStateAsync');
    expect(queries).toContain('ExportHandler_UnavailablePersonalDataReturnsNoPartialPartyPayloadAsync');
  });
});

const readRepositoryFile = (relativePath: string): string => {
  const absolutePath = resolve(REPOSITORY_ROOT, relativePath);
  expect(existsSync(absolutePath), `${relativePath} should exist`).toBe(true);

  return readFileSync(absolutePath, 'utf8');
};
