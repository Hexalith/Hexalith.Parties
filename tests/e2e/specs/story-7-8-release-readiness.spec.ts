import { expect, test } from '@playwright/test';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const SPEC_DIR = dirname(fileURLToPath(import.meta.url));
const REPOSITORY_ROOT = resolve(SPEC_DIR, '../../..');
const STORY_PATH = '_bmad-output/implementation-artifacts/7-8-release-rollback-cleanup-and-readiness-gate.md';
const SPRINT_STATUS_PATH = '_bmad-output/implementation-artifacts/sprint-status.yaml';
const READINESS_PATH = '_bmad-output/planning-artifacts/epic-7-final-readiness-2026-06-29.md';
const LOCAL_ADAPTER_PATH = 'src/Hexalith.Parties.Projections/Services/LocalPartyProjectionPlatformAdapter.cs';
const ADAPTER_MODE_PATH = 'src/Hexalith.Parties.Projections/Configuration/PartyProjectionPlatformAdapterMode.cs';
const REBUILD_SERVICE_PATH = 'src/Hexalith.Parties.Projections/Services/ProjectionRebuildService.cs';
const SERVICE_REGISTRATION_PATH = 'src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs';
const PAYLOAD_ADAPTER_PATH = 'src/Hexalith.Parties.Security/EventStorePartyPayloadProtectionAdapter.cs';
const PAYLOAD_SERVICE_PATH = 'src/Hexalith.Parties.Security/PartyPayloadProtectionService.cs';
const UI_FIXTURE_PATH = 'src/Hexalith.Parties.UI/Services/PartiesAdminPortalE2eFixture.cs';
const TEST_SUMMARY_PATH = '_bmad-output/implementation-artifacts/tests/test-summary.md';

const REQUIRED_READINESS_SECTIONS = [
  'Decision',
  'Baseline',
  'Story Evidence',
  'Repository And Package State',
  'Validation Matrix',
  'Public Compatibility Notes',
  'Cleanup Decisions',
  'Rollback Set',
  'KMS And Data Protection',
  'Release Blockers',
];

const REQUIRED_REPOSITORY_PATHS = [
  'references/Hexalith.AI.Tools',
  'references/Hexalith.Builds',
  'references/Hexalith.Commons',
  'references/Hexalith.EventStore',
  'references/Hexalith.FrontComposer',
  'references/Hexalith.Memories',
  'references/Hexalith.PolymorphicSerializations',
  'references/Hexalith.Tenants',
];

const REQUIRED_VALIDATION_RESULTS = new Map<string, string>([
  ['git diff --check', 'Pass'],
  ['bash scripts/check-no-warning-override.sh', 'Pass'],
  ['dotnet build Hexalith.Parties.slnx -c Release --no-restore -m:1 -p:NuGetAudit=false', 'Fail'],
  ['dotnet tests/Hexalith.Parties.Security.Tests/bin/Release/net10.0/Hexalith.Parties.Security.Tests.dll', 'Pass'],
  ['dotnet tests/Hexalith.Parties.Projections.Tests/bin/Release/net10.0/Hexalith.Parties.Projections.Tests.dll', 'Pass'],
  ['dotnet tests/Hexalith.Parties.Tests/bin/Release/net10.0/Hexalith.Parties.Tests.dll', 'Pass'],
  ['dotnet tests/Hexalith.Parties.UI.Tests/bin/Release/net10.0/Hexalith.Parties.UI.Tests.dll', 'Fail'],
  ['dotnet tests/Hexalith.Parties.Client.Tests/bin/Release/net10.0/Hexalith.Parties.Client.Tests.dll', 'Fail'],
  ['dotnet tests/Hexalith.Parties.Contracts.Tests/bin/Release/net10.0/Hexalith.Parties.Contracts.Tests.dll', 'Fail'],
]);

test.describe('Story 7.8 release rollback cleanup and readiness gate', () => {
  test('publishes final readiness evidence and preserves the Epic 7 status trail', () => {
    const story = readRepositoryFile(STORY_PATH);
    const sprintStatus = readRepositoryFile(SPRINT_STATUS_PATH);
    const readiness = readRepositoryFile(READINESS_PATH);

    expect(story).toMatch(/Status:\s+(review|done)/);
    expect(sprintStatus).toMatch(/7-8-release-rollback-cleanup-and-readiness-gate:\s+(review|done)/);

    for (const storyId of ['7-1', '7-2', '7-3', '7-4', '7-5', '7-6', '7-7']) {
      expect(sprintStatus).toMatch(new RegExp(`${storyId}-.+:\\s+done`));
      expect(readiness).toContain(`| ${storyId.replace('-', '.')} | Done |`);
    }

    for (const section of REQUIRED_READINESS_SECTIONS) {
      expect(readiness).toContain(`## ${section}`);
    }

    expect(readiness).toContain('Epic 7 remains post-MVP platform maintenance');
    expect(readiness).toContain('It adds no PRD functional requirement coverage');
  });

  test('pins every root repository and package-management decision with release status', () => {
    const readiness = readRepositoryFile(READINESS_PATH);
    const repositoryRows = parseMarkdownRows(readiness).filter((row) => REQUIRED_REPOSITORY_PATHS.includes(stripTicks(row[0])));

    expect(repositoryRows.map((row) => stripTicks(row[0]))).toEqual(REQUIRED_REPOSITORY_PATHS);

    for (const row of repositoryRows) {
      expect(row).toHaveLength(5);
      expect(row[1]).toMatch(/[0-9a-f]{40}/);
      expect(row[2]).toMatch(/[0-9a-f]{40}/);
      expect(row[3]).toMatch(/Clean|Gitlink drift/);
      expect(row[4]).toMatch(/Accept current pointer|Pre-release hygiene|Release blocker|reset candidate/i);
    }

    expect(readiness).toContain('Central Package Management remains intact');
    expect(readiness).toContain('No `.csproj` files contain `PackageReference Version=`');
    expect(readiness).toContain('Package versions remain centralized in `Directory.Packages.props`');
    expect(readiness).toContain('No classic `.sln` file was introduced');
    expect(readiness).toContain('No package version changes were made by Story 7.8');
  });

  test('records required validation lanes with exact blocked release decisions', () => {
    const readiness = readRepositoryFile(READINESS_PATH);
    const validationRows = parseMarkdownRows(readiness).filter((row) => REQUIRED_VALIDATION_RESULTS.has(stripTicks(row[0])));

    expect(validationRows).toHaveLength(REQUIRED_VALIDATION_RESULTS.size);

    for (const row of validationRows) {
      const command = stripTicks(row[0]);
      expect(row[1]).toBe(REQUIRED_VALIDATION_RESULTS.get(command));
      expect(row[2].trim().length).toBeGreaterThan(0);
    }

    expect(readiness).toContain('Scope: current submodule drift. Release decision: blocker.');
    expect(readiness).toContain('Release decision: blocker until rerun completes.');
    expect(readiness).toContain('Release decision: blocker or FrontComposer pointer reset/owner validation required.');
    expect(readiness).toContain('Release decision: blocked in sandbox, rerun in network-enabled package validation environment.');
    expect(readiness).toContain('Drifted root gitlinks must be owner-validated or reset before a release candidate is tagged.');
  });

  test('preserves rollback-only projection, crypto, and UI cleanup paths until proof exists', () => {
    const readiness = readRepositoryFile(READINESS_PATH);
    const localAdapter = readRepositoryFile(LOCAL_ADAPTER_PATH);
    const adapterMode = readRepositoryFile(ADAPTER_MODE_PATH);
    const rebuildService = readRepositoryFile(REBUILD_SERVICE_PATH);
    const registrations = readRepositoryFile(SERVICE_REGISTRATION_PATH);
    const payloadAdapter = readRepositoryFile(PAYLOAD_ADAPTER_PATH);
    const payloadService = readRepositoryFile(PAYLOAD_SERVICE_PATH);
    const uiFixture = readRepositoryFile(UI_FIXTURE_PATH);

    expect(readiness).toContain('LocalPartyProjectionPlatformAdapter');
    expect(readiness).toContain('PartyProjectionPlatformAdapterMode.Local');
    expect(readiness).toContain('Actor companion `last-sequence` keys');
    expect(readiness).toContain('ProjectionRebuildService');
    expect(readiness).toContain('PartyPayloadProtectionService');
    expect(readiness).toContain('PartiesAdminPortalE2eFixture');
    expect(readiness).toContain('Deferred');
    expect(readiness).toContain('Preserved');

    expect(localAdapter).toContain('LocalPartyProjectionPlatformAdapter');
    expect(adapterMode).toContain('Local');
    expect(rebuildService).toContain('EnsureTrustedPartyIdsAvailable');
    expect(rebuildService).toContain('GetProcessingRecordsAsync');
    expect(registrations).toContain('PartyProjectionPlatformAdapterMode.Local');
    expect(registrations).toContain('EventStorePartyPayloadProtectionAdapter');
    expect(payloadAdapter).toContain('PartyPayloadProtectionService inner');
    expect(payloadService).toContain('ProtectedSerializationFormat');
    expect(uiFixture).toContain('StripDiacritics');
  });

  test('keeps public compatibility, rollback, and KMS guardrails explicit', () => {
    const readiness = readRepositoryFile(READINESS_PATH);

    for (const rollback of ['Utility rollback', 'Search rollback', 'Projection rollback', 'Crypto rollback', 'Release rollback']) {
      expect(readiness).toContain(`- ${rollback}:`);
    }

    expect(readiness).toContain('Command/query contracts');
    expect(readiness).toContain('PagedResult<T>');
    expect(readiness).toContain('DAPR `/process` assumptions');
    expect(readiness).toContain('GDPR legal semantics');
    expect(readiness).toContain('LocalDevKeyStorageBackend');
    expect(readiness).toContain('Regulated EU personal data remains unauthorized without a real KMS');
    expect(readiness).toContain('did not replace production key management');
  });

  test('keeps generated QA evidence discoverable from the story file list and summary', () => {
    const story = readRepositoryFile(STORY_PATH);
    const summary = readRepositoryFile(TEST_SUMMARY_PATH);
    const fileList = extractStoryFileList(story);

    expect(fileList).toContain('tests/e2e/specs/story-7-8-release-readiness.spec.ts');
    expect(fileList).toContain('tests/e2e/specs/story-7-4-projection-platform-compatibility.spec.ts');
    expect(fileList).toContain('_bmad-output/implementation-artifacts/tests/test-summary.md');
    expect(summary).toContain('story-7-8-release-readiness.spec.ts');
    expect(summary).toContain('story-7-4-projection-platform-compatibility.spec.ts');
    expect(summary).toContain('Story 7.8 release readiness artifact validation');
    expect(summary).toContain('Release remains blocked by documented implementation blockers');
  });
});

const readRepositoryFile = (relativePath: string): string => {
  const absolutePath = resolve(REPOSITORY_ROOT, relativePath);
  expect(existsSync(absolutePath), `${relativePath} should exist`).toBe(true);

  return readFileSync(absolutePath, 'utf8');
};

const parseMarkdownRows = (markdown: string): string[][] =>
  markdown
    .split(/\r?\n/)
    .filter((line) => line.startsWith('|'))
    .map((line) => line.slice(1, -1).split('|').map((cell) => cell.trim()))
    .filter((row) => row.length > 1 && !row.every((cell) => /^-+$/.test(cell)));

const stripTicks = (value: string): string => value.replaceAll('`', '');

const extractStoryFileList = (story: string): string[] => {
  const match = story.match(/### File List\r?\n\r?\n(?<body>(?:- .+\r?\n)+)/);
  expect(match?.groups?.body, 'story file list should be present').toBeTruthy();

  return match?.groups?.body
    .trim()
    .split(/\r?\n/)
    .map((line) => line.replace(/^- `(?<path>.+)`$/, '$<path>')) ?? [];
};
