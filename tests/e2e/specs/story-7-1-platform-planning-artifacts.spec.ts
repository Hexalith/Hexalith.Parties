import { expect, test } from '@playwright/test';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const SPEC_DIR = dirname(fileURLToPath(import.meta.url));
const REPOSITORY_ROOT = resolve(SPEC_DIR, '../../..');
const ADR_PATH = '_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md';
const RELEASE_PLAN_PATH = '_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md';
const STORY_PATH = '_bmad-output/implementation-artifacts/7-1-platform-target-destination-adr-and-release-rollback-plan.md';
const EXPECTED_IDS = ['B1', 'B2', 'B3', 'B4', 'B5', 'B6', 'B7', 'B8', 'B9', 'B10', 'B11'];
const REQUIRED_ADR_SECTIONS = [
  'Context',
  'Decision',
  'Target Destination Matrix',
  'Dependency and Reference Strategy',
  'Missing Shared API Stories',
  'Compatibility Strategy',
  'Rollback Strategy',
  'Test Evidence Requirements',
  'Consequences',
];
const REQUIRED_RELEASE_GATES = [
  'ADR gate',
  'Owner API gate',
  'Reference gate',
  'Adapter gate',
  'Parity gate',
  'Rollback gate',
  'Privacy gate',
];
const ALLOWED_STORY_FILE_LIST = [
  '_bmad-output/implementation-artifacts/7-1-platform-target-destination-adr-and-release-rollback-plan.md',
  '_bmad-output/implementation-artifacts/sprint-status.yaml',
  '_bmad-output/planning-artifacts/adr-epic-7-platform-target-destinations.md',
  '_bmad-output/planning-artifacts/epic-7-release-rollback-plan-2026-06-29.md',
  'tests/e2e/specs/story-7-1-platform-planning-artifacts.spec.ts',
];

test.describe('Story 7.1 platform planning artifacts', () => {
  test('publishes the accepted ADR and release plan artifacts', () => {
    const adr = readRepositoryFile(ADR_PATH);
    const releasePlan = readRepositoryFile(RELEASE_PLAN_PATH);

    expect(adr).toContain('status: Accepted');
    expect(releasePlan).toContain('status: approved');

    for (const section of REQUIRED_ADR_SECTIONS) {
      expect(adr).toContain(`## ${section}`);
    }

    expect(releasePlan).toContain('`7.1 -> 7.2/7.3 -> 7.4 -> 7.5 -> 7.6 -> 7.7 -> 7.8`');
    expect(releasePlan).toContain('## Adoption Clusters');
    expect(releasePlan).toContain('## Final Readiness Evidence for Story 7.8');
    expect(releasePlan).toContain('## Scope Guard');
  });

  test('maps every Class B item exactly once with owner, reference, rollback, and evidence', () => {
    const adr = readRepositoryFile(ADR_PATH);
    const rows = parseMarkdownRows(adr).filter((row) => EXPECTED_IDS.includes(row[0]));

    expect(rows.map((row) => row[0])).toEqual(EXPECTED_IDS);

    for (const row of rows) {
      expect(row).toHaveLength(9);
      for (const cell of row) {
        expect(cell.trim().length).toBeGreaterThan(0);
        expect(cell).not.toMatch(/\bTBD\b/i);
      }

      const [, scope, owner, destinationSurface, referencePath, adapter, releaseOrder, rollback, evidence] = row;
      expect(scope).not.toMatch(/\bunknown\b/i);
      expect(owner).toMatch(/Hexalith|Parties|Commons|EventStore|FrontComposer|Memories/i);
      expect(destinationSurface).not.toMatch(/\bunowned\b/i);
      expect(referencePath).toMatch(/\$\(Hexalith|Existing Parties|references|project reference|root propert/i);
      expect(adapter).toMatch(/adapter|facade|wrapper|mapper|local|facades/i);
      expect(releaseOrder).toMatch(/7\.[2-8]/);
      expect(rollback).toMatch(/revert|restore|roll back|rollback|keep/i);
      expect(evidence).toMatch(/test|parity|harness|evidence|compatibility|irreversibility|redaction|leakage/i);
    }
  });

  test('routes missing shared APIs to owner stories before Parties adoption', () => {
    const adr = readRepositoryFile(ADR_PATH);

    expect(adr).toContain('Do not consume not-yet-released or unapproved APIs from a local checkout');
    expect(adr).toContain('Land additive');

    for (const storyId of ['7.2', '7.3', '7.4', '7.5', '7.6', '7.7', '7.8']) {
      expect(adr).toContain(`| ${storyId} |`);
    }

    expect(adr).toContain('Commons root MSBuild property');
    expect(adr).toContain('EventStore projection compatibility gaps');
    expect(adr).toContain('Crypto/key-management split');
  });

  test('pins the Commons project-reference strategy without authorizing package or submodule changes', () => {
    const adr = readRepositoryFile(ADR_PATH);
    const story = readRepositoryFile(STORY_PATH);

    expect(adr).toContain('HexalithCommonsRoot');
    expect(adr).toContain('ProjectReference Include="$(HexalithCommonsRoot)');
    expect(adr).toContain('versions only in `Directory.Packages.props`');
    expect(adr).toContain('`.csproj` files remain versionless');
    expect(adr).toContain('Story 7.1 intentionally does not edit `Directory.Build.props`');

    const fileList = extractStoryFileList(story);
    expect(fileList).toEqual(ALLOWED_STORY_FILE_LIST);
    expect(story).toContain('no production code migration, package upgrade, project-reference change, solution change, submodule pointer change, or submodule source edit');
  });

  test('requires release gates, rollback sets, privacy preservation, and Story 7.8 readiness evidence', () => {
    const releasePlan = readRepositoryFile(RELEASE_PLAN_PATH);

    for (const gate of REQUIRED_RELEASE_GATES) {
      expect(releasePlan).toContain(`| ${gate} |`);
    }

    expect(releasePlan).toContain('No story may rely on deleting local code as its rollback mechanism');
    expect(releasePlan).toContain('EventStore gateway routing');
    expect(releasePlan).toContain('GDPR erasure');
    expect(releasePlan).toContain('crypto-shredding');
    expect(releasePlan).toContain('PII-free logs/telemetry');
    expect(releasePlan).toContain('stale/degraded fallback');
    expect(releasePlan).toContain('Public Parties packages/contracts/UI behavior remain compatible');
    expect(releasePlan).toContain('Final submodule commit hashes or package versions');
    expect(releasePlan).toContain('deferred deletion list');
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

const extractStoryFileList = (story: string): string[] => {
  const match = story.match(/### File List\r?\n\r?\n(?<body>(?:- .+\r?\n)+)/);
  expect(match?.groups?.body, 'story file list should be present').toBeTruthy();

  return match?.groups?.body
    .trim()
    .split(/\r?\n/)
    .map((line) => line.replace(/^- `(?<path>.+)`$/, '$<path>')) ?? [];
};
