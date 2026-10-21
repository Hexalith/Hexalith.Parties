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
      include: ['#fc-main-content'],
      requiredSelectors: [
        "[data-testid='parties-accessibility-specimen-ready']",
        '#fc-main-content',
        '#fc-nav',
        "a[href$='#fc-main-content']",
        "a[href$='#fc-nav']",
      ],
      artifactPath: testInfo.outputPath('axe-parties-accessibility.json'),
    });
  });

  test('route-heading announcement leaves skip links forward reachable and focuses their targets', async ({ page }) => {
    await gotoSpecimen(page);
    await expect(page.locator('#parties-accessibility-specimen-title')).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(page.locator(':focus')).toHaveText('Skip to content');
    await page.keyboard.press('Enter');
    await expect(page.locator(':focus')).toHaveAttribute('id', 'fc-main-content');
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('parties-specimen-primary-action')).toBeFocused();

    await gotoSpecimen(page);
    await expect(page.locator('#parties-accessibility-specimen-title')).toBeFocused();
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    await expect(page.locator(':focus')).toHaveText('Skip to navigation');
    await page.keyboard.press('Enter');
    await expect(page.locator(':focus')).toHaveAttribute('id', 'fc-nav');
  });

  test('client-side route focus restores forward skip access', async ({ page }) => {
    await gotoSpecimen(page);
    await page.evaluate(() => {
      document.documentElement.setAttribute('data-dw91-document', 'original');
      document.querySelector('.fc-shell-root')!.setAttribute('data-dw91-shell', 'original');
    });

    // Navigate between different page components: a query-only change can keep the original
    // heading focused without exercising FocusOnNavigate or a new focus announcement.
    for (const route of ['/parties', `${SPECIMEN_ROUTE}?dw91=client-navigation`]) {
      await page.evaluate((href) => {
        document.querySelector('[data-testid="dw91-client-route"]')?.remove();
        const link = document.createElement('a');
        link.href = href;
        link.textContent = 'Navigate to next accessibility route';
        link.dataset.testid = 'dw91-client-route';
        document.querySelector('#fc-main-content')!.append(link);
      }, route);
      await page.getByTestId('dw91-client-route').click();
      await expect(page).toHaveURL(new RegExp(`${route.replace(/[?]/g, '\\?')}$`));
      await expect(page.locator('html')).toHaveAttribute('data-dw91-document', 'original');
      await expect(page.locator('.fc-shell-root')).toHaveAttribute('data-dw91-shell', 'original');
      await expect(page.locator('#fc-main-content h1')).toBeFocused();
      if (route === '/parties') {
        await expect(page.locator('#parties-accessibility-specimen-title')).toHaveCount(0);
      }
    }

    await page.getByTestId('dw91-client-route').evaluate((link) => link.remove());
    await expect(page.locator('#parties-accessibility-specimen-title')).toBeFocused();
    await page.keyboard.press('Tab');
    await expect(page.getByRole('link', { name: 'Skip to content', exact: true })).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page.locator('#fc-main-content')).toBeFocused();
    await expect(page).toHaveURL(/\?dw91=client-navigation$/);
    await page.keyboard.press('Tab');
    await expect(page.getByTestId('parties-specimen-primary-action')).toBeFocused();
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

    await page.locator(".fc-skip-link[href$='#fc-main-content']").focus();
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
  await expect(page.locator('.fc-shell-root[data-fc-interactive="true"]')).toBeVisible();
  const text = await page.locator('body').innerText();
  expect(text.trim().length, `${SPECIMEN_ROUTE} rendered blank text`).toBeGreaterThan(0);
};

const tabUntilTestId = async (page: import('@playwright/test').Page, testId: string): Promise<void> => {
  const focusSequence: string[] = [];
  const target = page.getByTestId(testId);
  for (let attempt = 0; attempt < 60; attempt += 1) {
    await page.keyboard.press('Tab');
    const targetHasFocus = await target.evaluate((element) => {
      let active = document.activeElement;
      while (active?.shadowRoot?.activeElement) {
        active = active.shadowRoot.activeElement;
      }

      return active === element || element.contains(active);
    });
    if (targetHasFocus) {
      return;
    }

    const focused = await page.evaluate(() => {
      let element = document.activeElement;
      while (element?.shadowRoot?.activeElement) {
        element = element.shadowRoot.activeElement;
      }

      if (!(element instanceof HTMLElement)) {
        return { description: '<no active element>' };
      }

      // `||`, not `??`: element.id is '' when absent, which is not nullish, so a `??` chain stops
      // there and every diagnostic line renders blank.
      return {
        description: element.dataset.testid
          || element.id
          || element.getAttribute('aria-label')
          || element.textContent?.trim().slice(0, 60)
          || element.tagName,
      };
    });
    focusSequence.push(focused.description);
  }

  throw new Error(`Expected focus to reach ${testId}; observed: ${focusSequence.join(' -> ')}`);
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
  skipLinks: await page.locator('.fc-skip-link').evaluateAll((links) =>
    links.map((link) => link.textContent?.trim() ?? '')),
  landmarks: {
    main: await page.locator('#fc-main-content').evaluate((element) => element.getAttribute('aria-label') ?? ''),
    navigation: await page.locator("[data-testid='fc-navigation-rail']:visible").evaluate((element) => element.getAttribute('aria-label') ?? ''),
  },
  specimenSections: await page.locator('.parties-accessibility-specimen h1, .parties-accessibility-specimen h2').evaluateAll((headings) =>
    headings.map((heading) => heading.textContent?.trim() ?? '')),
  representativeControls: {
    primaryButtonVisible: await page.getByTestId('parties-specimen-primary-action').isVisible(),
    normalLinkVisible: await page.getByTestId('parties-specimen-link').isVisible(),
    // Assert the ARIA contract, not the copy. The specimen renders several polite regions, so scope
    // to the first rather than matching text -- a text match would let the live-region semantics
    // regress to a plain <div> and still pass.
    statusRegionVisible: await page.locator("[role='status'][aria-live='polite']").first().isVisible(),
    freshnessVisible: await page.locator('.data-freshness-indicator').isVisible(),
    stateBadgeVisible: await page.locator('.party-state-badge').isVisible(),
    destructiveButtonVisible: await page.locator('.gdpr-destructive-button').isVisible(),
  },
  primaryButton: {
    avoidsRawTeal: await page.getByTestId('parties-specimen-primary-action')
      .evaluate((element) => getComputedStyle(element).backgroundColor !== 'rgb(0, 151, 167)'),
  },
});
