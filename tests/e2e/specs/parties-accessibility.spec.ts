import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { expectNoBlockingAxeViolations } from '../helpers/a11y.js';

const SPECIMEN_ROUTE = '/__parties/specimens/accessibility';
const VISUAL_BASELINE = new URL('../baselines/parties-accessibility-shell.visual.json', import.meta.url);

test.describe('Parties UI accessibility specimen', () => {
  test.use({
    locale: 'en-US',
    timezoneId: 'UTC',
    deviceScaleFactor: 1,
  });

  test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.emulateMedia({ reducedMotion: 'reduce' });
  });

  test('specimen is nonblank and passes the blocking axe gate', async ({ page }, testInfo) => {
    await gotoSpecimen(page);

    await expectNoBlockingAxeViolations(page, {
      route: SPECIMEN_ROUTE,
      include: ['#parties-main-content'],
      requiredSelectors: [
        "[data-testid='parties-accessibility-specimen-ready']",
        '#parties-main-content',
        '#parties-app-navigation',
        "a[href='#parties-main-content']",
        "a[href='#parties-app-navigation']",
      ],
      artifactPath: testInfo.outputPath('axe-parties-accessibility.json'),
    });
  });

  test('skip links are the first two keyboard tab stops and focus their targets', async ({ page }) => {
    await gotoSpecimen(page);

    await page.keyboard.press('Tab');
    await expect(page.locator(':focus')).toHaveText('Skip to content');
    await page.keyboard.press('Enter');
    await expect(page.locator(':focus')).toHaveAttribute('id', 'parties-main-content');

    await gotoSpecimen(page);
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    await expect(page.locator(':focus')).toHaveText('Skip to navigation');
    await page.keyboard.press('Enter');
    await expect(page.locator(':focus')).toHaveAttribute('id', 'parties-app-navigation');
  });

  test('keyboard flow reaches representative controls without trapping focus', async ({ page }) => {
    await gotoSpecimen(page);

    for (const testId of ['parties-specimen-primary-action', 'parties-specimen-link']) {
      await tabUntilTestId(page, testId);
      await expect(page.locator(':focus')).toBeInViewport();
    }
  });

  test('forced-colors and reduced-motion media are observable', async ({ browser }) => {
    const context = await browser.newContext({
      baseURL: process.env.BASE_URL ?? 'http://127.0.0.1:5072',
      forcedColors: 'active',
      reducedMotion: 'reduce',
      ignoreHTTPSErrors: true,
    });
    const page = await context.newPage();

    await gotoSpecimen(page);

    expect(await page.evaluate(() => matchMedia('(forced-colors: active)').matches)).toBe(true);
    expect(await page.evaluate(() => matchMedia('(prefers-reduced-motion: reduce)').matches)).toBe(true);

    await page.getByText('Skip to content').focus();
    const outlineColor = await page.locator(':focus').evaluate((element) => getComputedStyle(element).outlineColor);
    expect(outlineColor).toBeTruthy();

    await context.close();
  });

  test('filled primary button does not compute to raw teal', async ({ page }) => {
    await gotoSpecimen(page);

    const button = page.getByTestId('parties-specimen-primary-action');
    const backgroundColor = await button.evaluate((element) => getComputedStyle(element).backgroundColor);

    expect(backgroundColor).not.toBe('rgb(0, 151, 167)');
    await expect(button).toBeVisible();
  });

  test('visual baseline for deterministic shell specimen', async ({ page }, testInfo) => {
    await gotoSpecimen(page);

    const screenshot = await page.screenshot({
      fullPage: true,
      animations: 'disabled',
    });
    await testInfo.attach('parties-accessibility-shell.png', {
      body: screenshot,
      contentType: 'image/png',
    });
    expect(screenshot.byteLength, 'shell specimen screenshot should not be blank or tiny').toBeGreaterThan(10_000);

    const expected = JSON.parse(await readFile(VISUAL_BASELINE, 'utf8')) as PartiesVisualBaseline;
    expect(await collectVisualBaseline(page)).toEqual(expected);
  });
});

const gotoSpecimen = async (page: import('@playwright/test').Page): Promise<void> => {
  await page.goto(SPECIMEN_ROUTE);
  await expect(page.getByTestId('parties-accessibility-specimen-ready'), `${SPECIMEN_ROUTE} missing ready marker`).toBeVisible();
  const text = await page.locator('body').innerText();
  expect(text.trim().length, `${SPECIMEN_ROUTE} rendered blank text`).toBeGreaterThan(0);
};

const tabUntilTestId = async (page: import('@playwright/test').Page, testId: string): Promise<void> => {
  for (let attempt = 0; attempt < 30; attempt += 1) {
    await page.keyboard.press('Tab');
    const focusedTestId = await page.locator(':focus').getAttribute('data-testid');
    if (focusedTestId === testId) {
      return;
    }
  }

  throw new Error(`Expected focus to reach ${testId}`);
};

interface PartiesVisualBaseline {
  route: string;
  viewport: {
    width: number;
    height: number;
  };
  skipLinks: string[];
  landmarks: {
    main: string;
    navigation: string;
  };
  specimenSections: string[];
  representativeControls: {
    primaryButtonVisible: boolean;
    normalLinkVisible: boolean;
    statusRegionVisible: boolean;
    freshnessVisible: boolean;
    stateBadgeVisible: boolean;
    destructiveButtonVisible: boolean;
  };
  primaryButton: {
    avoidsRawTeal: boolean;
  };
}

const collectVisualBaseline = async (page: import('@playwright/test').Page): Promise<PartiesVisualBaseline> => ({
  route: SPECIMEN_ROUTE,
  viewport: await page.viewportSize() ?? { width: 0, height: 0 },
  skipLinks: await page.locator('.parties-skip-link').evaluateAll((links) =>
    links.map((link) => link.textContent?.trim() ?? '')),
  landmarks: {
    main: await page.locator('#parties-main-content').evaluate((element) => element.getAttribute('aria-label') ?? ''),
    navigation: await page.locator('#parties-app-navigation').evaluate((element) => element.getAttribute('aria-label') ?? ''),
  },
  specimenSections: await page.locator('.parties-accessibility-specimen h1, .parties-accessibility-specimen h2').evaluateAll((headings) =>
    headings.map((heading) => heading.textContent?.trim() ?? '')),
  representativeControls: {
    primaryButtonVisible: await page.getByTestId('parties-specimen-primary-action').isVisible(),
    normalLinkVisible: await page.getByTestId('parties-specimen-link').isVisible(),
    statusRegionVisible: await page.locator("[role='status'][aria-live='polite']").isVisible(),
    freshnessVisible: await page.locator('.data-freshness-indicator').isVisible(),
    stateBadgeVisible: await page.locator('.party-state-badge').isVisible(),
    destructiveButtonVisible: await page.locator('.gdpr-destructive-button').isVisible(),
  },
  primaryButton: {
    avoidsRawTeal: await page.getByTestId('parties-specimen-primary-action')
      .evaluate((element) => getComputedStyle(element).backgroundColor !== 'rgb(0, 151, 167)'),
  },
});
