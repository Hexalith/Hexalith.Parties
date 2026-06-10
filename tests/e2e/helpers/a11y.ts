import AxeBuilder from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';
import { mkdir, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';

export interface A11yOptions {
  tags?: string[];
  disableRules?: string[];
  include?: string[];
  exclude?: string[];
  route?: string;
  artifactPath?: string;
  requiredSelectors?: string[];
}

const WCAG_AA_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;
const BLOCKING_IMPACTS = new Set(['serious', 'critical']);
const REPORT_ONLY_IMPACTS = new Set(['minor', 'moderate']);

export const expectNoBlockingAxeViolations = async (page: Page, options: A11yOptions = {}): Promise<void> => {
  for (const selector of options.requiredSelectors ?? []) {
    const count = await page.locator(selector).count();
    expect(count, `${options.route ?? page.url()} missing required selector ${selector}`).toBeGreaterThan(0);
  }

  let builder = new AxeBuilder({ page }).withTags(options.tags ?? [...WCAG_AA_TAGS]);
  if (options.disableRules?.length) builder = builder.disableRules(options.disableRules);
  for (const selector of options.include ?? []) builder = builder.include(selector);
  for (const selector of options.exclude ?? []) builder = builder.exclude(selector);

  const result = await builder.analyze();
  const scannedNodes = result.passes.reduce((count, pass) => count + pass.nodes.length, 0)
    + result.incomplete.reduce((count, incomplete) => count + incomplete.nodes.length, 0)
    + result.violations.reduce((count, violation) => count + violation.nodes.length, 0);
  expect(scannedNodes, `${options.route ?? page.url()} axe scan included zero target nodes`).toBeGreaterThan(0);

  const partitioned = partitionAxeViolations(result.violations);
  if (options.artifactPath) {
    await writeAxeSummary(options.artifactPath, {
      route: options.route ?? page.url(),
      blocking: partitioned.blocking,
      reportOnly: partitioned.reportOnly,
      unknown: partitioned.unknown,
    });
  }

  expect(partitioned.blocking, formatViolations(partitioned.blocking)).toEqual([]);
  expect(partitioned.unknown, `Unexpected axe impact values must be triaged explicitly:\n${formatViolations(partitioned.unknown)}`).toEqual([]);
};

export type AxeViolation = Awaited<ReturnType<AxeBuilder['analyze']>>['violations'][number];

export interface AxeViolationPartition {
  blocking: AxeViolation[];
  reportOnly: AxeViolation[];
  unknown: AxeViolation[];
}

export const partitionAxeViolations = (violations: AxeViolation[]): AxeViolationPartition => ({
  blocking: violations.filter((violation) => BLOCKING_IMPACTS.has(violation.impact ?? '')),
  reportOnly: violations.filter((violation) => REPORT_ONLY_IMPACTS.has(violation.impact ?? '')),
  unknown: violations.filter((violation) => !BLOCKING_IMPACTS.has(violation.impact ?? '') && !REPORT_ONLY_IMPACTS.has(violation.impact ?? '')),
});

const formatViolations = (violations: AxeViolation[]): string => {
  if (violations.length === 0) return 'no a11y violations';
  return violations
    .map((violation) => `${violation.id} [${violation.impact ?? 'unknown'}]: ${violation.help} (${violation.nodes.length} node(s))`)
    .join('\n');
};

interface AxeSummary {
  route: string;
  blocking: AxeViolation[];
  reportOnly: AxeViolation[];
  unknown: AxeViolation[];
}

const writeAxeSummary = async (artifactPath: string, summary: AxeSummary): Promise<void> => {
  await mkdir(dirname(artifactPath), { recursive: true });
  await writeFile(
    artifactPath,
    `${JSON.stringify({
      route: summary.route,
      blocking: summarizeViolations(summary.blocking),
      reportOnly: summarizeViolations(summary.reportOnly),
      unknown: summarizeViolations(summary.unknown),
    }, null, 2)}\n`,
    'utf8',
  );
};

const summarizeViolations = (violations: AxeViolation[]) =>
  violations.map((violation) => ({
    id: violation.id,
    impact: violation.impact ?? 'unknown',
    help: violation.help,
    helpUrl: violation.helpUrl,
    selectors: violation.nodes.slice(0, 10).map((node) => node.target.join(' ')),
    truncated: violation.nodes.length > 10,
  }));
