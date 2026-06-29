import { expect, test } from '@playwright/test';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const SPEC_DIR = dirname(fileURLToPath(import.meta.url));
const REPOSITORY_ROOT = resolve(SPEC_DIR, '../../..');
const STORY_PATH = '_bmad-output/implementation-artifacts/7-4-projection-platform-compatibility-adapter.md';
const ADAPTER_CONTRACT_PATH = 'src/Hexalith.Parties.Projections/Services/IPartyProjectionPlatformAdapter.cs';
const EVENTSTORE_ADAPTER_PATH = 'src/Hexalith.Parties/Domain/EventStorePartyProjectionPlatformAdapter.cs';
const LOCAL_ADAPTER_PATH = 'src/Hexalith.Parties.Projections/Services/LocalPartyProjectionPlatformAdapter.cs';
const PROJECTION_OPTIONS_PATH = 'src/Hexalith.Parties.Projections/Configuration/ProjectionOptions.cs';
const SERVICE_REGISTRATION_PATH = 'src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs';
const ORCHESTRATOR_PATH = 'src/Hexalith.Parties/Domain/PartyProjectionUpdateOrchestrator.cs';
const REBUILD_SERVICE_PATH = 'src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs';
const ADAPTER_TESTS_PATH = 'tests/Hexalith.Parties.Tests/Projections/ProjectionPlatformAdapterTests.cs';
const REBUILD_TESTS_PATH = 'tests/Hexalith.Parties.Tests/Projections/ProjectionRebuildServiceTests.cs';

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

  test('keeps the compatibility boundary local with an EventStore adapter and rollback adapter', () => {
    const contract = readRepositoryFile(ADAPTER_CONTRACT_PATH);
    const eventStoreAdapter = readRepositoryFile(EVENTSTORE_ADAPTER_PATH);
    const localAdapter = readRepositoryFile(LOCAL_ADAPTER_PATH);
    const options = readRepositoryFile(PROJECTION_OPTIONS_PATH);
    const registrations = readRepositoryFile(SERVICE_REGISTRATION_PATH);

    expect(contract).toContain('ReadDeliveredSequenceAsync');
    expect(contract).toContain('TrySaveDeliveredSequenceAsync');
    expect(contract).toContain('ReadRebuildCheckpointAsync');
    expect(contract).toContain('MapFreshness');

    expect(eventStoreAdapter).toContain('IProjectionCheckpointTracker');
    expect(eventStoreAdapter).toContain('IProjectionRebuildCheckpointStore');
    expect(eventStoreAdapter).toContain('ReadModelFreshnessState.Aging');
    expect(eventStoreAdapter).toContain('ProjectionFreshnessStatus.Current');
    expect(eventStoreAdapter).toContain('ProjectionFreshnessStatus.Unavailable');

    expect(localAdapter).toContain('LocalPartyProjectionPlatformAdapter');
    expect(localAdapter).toContain('ProjectionFreshnessStatus.Rebuilding');
    expect(localAdapter).toContain('rebuild-checkpoint');

    expect(options).toContain('PlatformAdapterMode');
    expect(options).toContain('PartyProjectionPlatformAdapterMode.EventStore');
    expect(registrations).toContain('PartyProjectionPlatformAdapterMode.Local');
    expect(registrations).toContain('EventStorePartyProjectionPlatformAdapter');
    expect(registrations).toContain('LocalPartyProjectionPlatformAdapter');
  });

  test('preserves replay-from-zero delivery and saves checkpoints only after both projections accept', () => {
    const orchestrator = readRepositoryFile(ORCHESTRATOR_PATH);
    const detailDeliveryIndex = orchestrator.indexOf('detailProjection');
    const indexDeliveryIndex = orchestrator.indexOf('indexProjection');
    const checkpointSaveIndex = orchestrator.indexOf('TrySaveDeliveredSequenceAsync(identity, envelope.SequenceNumber');

    expect(orchestrator).toContain('GetEventsAsync(0)');
    expect(orchestrator).toContain('events.OrderBy(static e => e.SequenceNumber)');
    expect(orchestrator).toContain('DuplicateSequenceDetected');
    expect(detailDeliveryIndex).toBeGreaterThanOrEqual(0);
    expect(indexDeliveryIndex).toBeGreaterThan(detailDeliveryIndex);
    expect(checkpointSaveIndex).toBeGreaterThan(indexDeliveryIndex);
  });

  test('routes rebuild checkpoints through the adapter without removing local replay mechanics', () => {
    const rebuildService = readRepositoryFile(REBUILD_SERVICE_PATH);

    expect(rebuildService).toContain('IPartyProjectionPlatformAdapter');
    expect(rebuildService).toContain('ReadRebuildCheckpointAsync');
    expect(rebuildService).toContain('SaveRebuildCheckpointAsync');
    expect(rebuildService).toContain('DeleteRebuildCheckpointAsync');
    expect(rebuildService).toContain('ReadAggregateEventRecordsAsync');
    expect(rebuildService).toContain('EnsureTrustedPartyIdsAvailable');
    expect(rebuildService).toContain('PartyEventTypeResolver.Resolve');
    expect(rebuildService).not.toContain('Type.GetType');
  });

  test('pins focused adapter and rebuild test coverage for the parity proof', () => {
    const adapterTests = readRepositoryFile(ADAPTER_TESTS_PATH);
    const rebuildTests = readRepositoryFile(REBUILD_TESTS_PATH);

    expect(adapterTests).toContain('AddParties_DefaultProjectionPlatformMode_UsesEventStoreAdapter');
    expect(adapterTests).toContain('AddParties_LocalProjectionPlatformMode_UsesRollbackAdapter');
    expect(adapterTests).toContain('EventStoreAdapter_SaveRebuildCheckpoint_MapsDetailScopeAndKeepsLocalCheckpointAsync');
    expect(adapterTests).toContain('EventStoreAdapter_DeleteRebuildCheckpoint_CompletionFailureSurfacesAfterLocalCleanupAsync');
    expect(adapterTests).toContain('ProjectionDelivery_OutOfOrderEvents_SavesPlatformCheckpointAfterBothActorsAcceptInSequenceAsync');
    expect(adapterTests).toContain('ProjectionDelivery_IndexFailure_DoesNotSavePlatformCheckpointAfterDetailOnlyAsync');

    expect(rebuildTests).toContain('RebuildDetailProjectionAsync_CheckpointExists_ResumesFromNextSequenceAsync');
    expect(rebuildTests).toContain('RebuildIndexProjectionAsync_MissingIndexAndManifest_FailsClosedWithoutWritingAsync');
    expect(rebuildTests).toContain('RebuildDetailProjectionAsync_StateWriteFailure_StopsBeforeCheckpointWriteAsync');
    expect(rebuildTests).toContain('GetProcessingRecordsAsync_ReturnsBoundedAuditMetadataWithoutPayloadTextAsync');
  });
});

const readRepositoryFile = (relativePath: string): string => {
  const absolutePath = resolve(REPOSITORY_ROOT, relativePath);
  expect(existsSync(absolutePath), `${relativePath} should exist`).toBe(true);

  return readFileSync(absolutePath, 'utf8');
};
