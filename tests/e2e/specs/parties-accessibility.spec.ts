import { expectAccordionFocusClearance } from '../helpers/accordion-focus.js';
import { expect, test } from '@playwright/test';
import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
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

    const assetPaths = [
      '/Hexalith.Parties.UI.styles.css',
      '/_content/Hexalith.FrontComposer.Shell/css/fc-shell.css',
      '/_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.bundle.scp.css',
    ];
    const assets = await Promise.all(assetPaths.map(async (path) => {
      const response = await page.request.get(path);
      expect(response.ok(), `served accessibility stylesheet ${path}`).toBe(true);
      const bytes = await response.body();
      return { path, sha256: createHash('sha256').update(bytes).digest('hex'), bytes: bytes.length };
    }));
    const server = testInfo.config.webServer;
    const identityPath = testInfo.outputPath('browser-identity.json');
    await writeFile(identityPath, `${JSON.stringify({
      browser: page.context().browser()?.version(),
      node: process.version,
      baseURL: testInfo.project.use.baseURL,
      server: server ? {
        mode: 'managed', command: server.command,
        environment: Object.fromEntries(Object.entries(server.env ?? {})
          .filter(([key]) => ['ASPNETCORE_ENVIRONMENT', 'Hexalith__Parties__AccessibilitySpecimens__Enabled',
            'Hexalith__Parties__AdminPortalE2E__Enabled', 'Parties__BaseUrl', 'Parties__Tenant'].includes(key))),
      } : { mode: 'external', environment: 'not observed' },
      assets,
    }, null, 2)}\n`);
    await testInfo.attach('browser-identity.json', { path: identityPath, contentType: 'application/json' });

    await auditSpecimen(page, testInfo.outputPath('axe-parties-accessibility.json'));
    for (const target of ['fc-main-content', 'fc-nav']) {
      const skipLink = page.locator(`.fc-skip-link[href$='#${target}']`);
      await skipLink.focus();
      await expect(skipLink).toBeFocused();
      await expect(skipLink).toBeInViewport();
      await auditSpecimen(page, testInfo.outputPath(`axe-exposed-skip-${target}.json`));
    }
  });

  for (const region of ['navigation', 'skip link'] as const) {
    test(`blocking axe gate rejects an inaccessible ${region} outside main content`, async ({ page }, testInfo) => {
      await gotoSpecimen(page);
      if (region === 'navigation') {
        await page.getByRole('navigation', { name: 'Primary navigation' }).evaluate((navigation) => {
          const button = document.createElement('button');
          button.dataset.testid = 'inaccessible-navigation-control';
          navigation.append(button);
        });
      } else {
        const skipLink = page.getByRole('link', { name: 'Skip to content', exact: true });
        await skipLink.evaluate((link) => {
          link.textContent = '';
          link.removeAttribute('aria-label');
          link.removeAttribute('aria-labelledby');
          link.removeAttribute('title');
        });
        // Shell skip links are offscreen until focused. Scan the visibly exposed link.
        await page.locator(".fc-skip-link[href$='#fc-main-content']").focus();
      }

      await expect(auditSpecimen(page,
        testInfo.outputPath(`axe-rejected-${region.replace(' ', '-')}.json`),
      )).rejects.toThrow(region === 'navigation' ? /button-name/ : /link-name/);
    });
  }

  // Always-on package coverage checks shell order and skip targets from its root.
  // Forward reachability from the announced route heading is qualified separately below;
  // those source regressions become unconditional after a fixed package is adopted (DW-91).
  test('skip links are the first two focusable elements in the shell and focus their targets', async ({ page }) => {
    await gotoSpecimen(page);
    await page.locator('.fc-shell-root').focus();

    await page.keyboard.press('Tab');
    await expect(page.locator(':focus')).toHaveText('Skip to content');
    await page.keyboard.press('Enter');
    await expect(page.locator(':focus')).toHaveAttribute('id', 'fc-main-content');

    await gotoSpecimen(page);
    await page.locator('.fc-shell-root').focus();
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    await expect(page.locator(':focus')).toHaveText('Skip to navigation');
    await page.keyboard.press('Enter');
    await expect(page.locator(':focus')).toHaveAttribute('id', 'fc-nav');
  });

  test('compact and resized layouts expose only mounted skip targets and preserve keyboard content access', async ({ page }) => {
    test.skip(process.env.PARTIES_E2E_COMPACT_SKIP_LINKS !== '1',
      'DW-112 requires the fixed FrontComposer source; matching package publication/adoption is pending');
    await gotoSpecimen(page);
    for (const width of [1280, 390, 800, 1280, 390, 1280]) {
      await page.setViewportSize({ width, height: 900 });
      const mounted = width >= 1024;
      await expect(page.locator('#fc-nav')).toHaveCount(mounted ? 1 : 0);
      await expect(page.locator(".fc-skip-link[href$='#fc-nav']")).toHaveCount(mounted ? 1 : 0);
      await expect(page.locator(".fc-skip-link[href$='#fc-main-content']")).toHaveCount(1);
      await expect(page.locator('[data-testid="fc-route-navigation-status"]')).toHaveAttribute('aria-live', 'polite');
      await page.locator('.fc-shell-root').focus();
      await page.keyboard.press('Tab');
      await expect(page.getByRole('link', { name: 'Skip to content', exact: true })).toBeFocused();
      await page.keyboard.press('Enter');
      await expect(page.locator('#fc-main-content')).toBeFocused();
      if (mounted) {
        await page.locator('.fc-shell-root').focus();
        await page.keyboard.press('Tab');
        await page.keyboard.press('Tab');
        await expect(page.getByRole('link', { name: 'Skip to navigation', exact: true })).toBeFocused();
        await page.keyboard.press('Enter');
        await expect(page.locator('#fc-nav')).toBeFocused();
      }
    }
  });

  test('route-heading announcement leaves skip links forward reachable and focuses their targets', async ({ page }) => {
    test.skip(process.env.PARTIES_E2E_FORWARD_SKIP_LINKS !== '1',
      'DW-91 source qualification requires the fixed FrontComposer shell; package adoption is pending');
    await gotoSpecimen(page);
    await expect(page.locator('#parties-accessibility-specimen-title')).toBeFocused();

    await page.keyboard.press('Tab');
    await expect(page.locator(':focus')).toHaveText('Skip to content');
    await page.keyboard.press('Enter');
    await expect(page.locator(':focus')).toHaveAttribute('id', 'fc-main-content');
    await page.keyboard.press('Tab');
    await expect(page.getByRole('button', { name: 'Representative controls', exact: true })).toBeFocused();
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
    test.skip(process.env.PARTIES_E2E_FORWARD_SKIP_LINKS !== '1',
      'DW-91 source qualification requires the fixed FrontComposer shell; package adoption is pending');
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

  test('accordion focus check rejects suppressed actual focus paint', async ({ page }) => {
    await gotoSpecimen(page);
    const control = page.getByTestId('parties-specimen-primary-action');
    await control.evaluate(element => {
      const suppress = (node: Element) => {
        if (node instanceof HTMLElement) {
          node.style.setProperty('outline', 'none', 'important');
          node.style.setProperty('box-shadow', 'none', 'important');
          node.style.setProperty('border-color', 'transparent', 'important');
        }
        if (node.shadowRoot) {
          const style = document.createElement('style');
          style.textContent = '*:focus, *:focus-visible, *::before, *::after { outline: none !important; box-shadow: none !important; border-color: transparent !important; }';
          node.shadowRoot.append(style);
        }
        for (const child of node.children) suppress(child);
        if (node.shadowRoot) for (const child of node.shadowRoot.children) suppress(child);
      };
      suppress(element);
    });
    await expect(expectAccordionFocusClearance(control)).rejects.toThrow(/actual focused control must paint/);
  });

  test('accordion focus check rejects an intermediate clipping wrapper', async ({ page }) => {
    await gotoSpecimen(page);
    const control = page.getByTestId('parties-specimen-primary-action');
    await control.evaluate(element => {
      const wrapper = document.createElement('div');
      wrapper.style.cssText = 'display: inline-block; overflow: hidden;';
      element.replaceWith(wrapper);
      wrapper.append(element);
    });
    await expect(expectAccordionFocusClearance(control)).rejects.toThrow(/clipping ancestor/);
  });

  test('disabled irreversible confirmation retains the Fluent disabled appearance', async ({ page }) => {
    await gotoSpecimen(page);
    const original = page.locator('.gdpr-destructive-button fluent-button');
    await original.evaluate(element => {
      const disabled = element.cloneNode(true) as HTMLElement;
      disabled.id = 'disabled-irreversible-appearance-control';
      disabled.setAttribute('appearance', 'primary');
      disabled.setAttribute('disabled', '');
      disabled.setAttribute('aria-disabled', 'true');
      element.parentElement!.append(disabled);
    });
    const disabled = page.locator('#disabled-irreversible-appearance-control');
    await expect(disabled).toBeDisabled();
    const paint = () => disabled.evaluate(element => {
      const style = getComputedStyle(element);
      return { background: style.backgroundColor, color: style.color, border: style.borderColor };
    });
    for (const state of ['normal', 'hover', 'active']) {
      if (state !== 'normal') await disabled.hover({ force: true });
      if (state === 'active') await page.mouse.down();
      const expected = await paint();
      await disabled.evaluate(element => element.classList.add('gdpr-destructive-button__button--danger'));
      expect(await paint(), `disabled ${state} appearance must remain component-owned`).toEqual(expected);
      await disabled.evaluate(element => element.classList.remove('gdpr-destructive-button__button--danger'));
      if (state === 'active') await page.mouse.up();
    }
  });

  for (const forcedColors of ['none', 'active'] as const) {
    test(`content controls have visible keyboard focus with forced colors ${forcedColors}`, async ({ page }, testInfo) => {
      await page.emulateMedia({ forcedColors });
      await gotoSpecimen(page);

      const observations = [];
      for (const testId of ['parties-specimen-primary-action', 'parties-specimen-link']) {
        await tabUntilTestId(page, testId);
        const control = page.getByTestId(testId);
        await expect(control).toBeFocused();
        await expect(control).toBeInViewport({ ratio: 1 });
        const focus = await control.evaluate((element, forced) => {
          const expected = document.createElement('span');
          expected.style.cssText = `outline: var(--strokeWidthThick) solid ${forced ? 'Highlight' : 'var(--colorStrokeFocus2)'}; outline-offset: var(--spacingHorizontalXS); background: Canvas; color: CanvasText;`;
          element.parentElement!.append(expected);
          const actualStyle = getComputedStyle(element);
          const expectedStyle = getComputedStyle(expected);
          // The outline sits outside the control. Composite ancestor fills so a
          // transparent link or a colored button cannot hide white-on-white focus.
          const canvas = document.createElement('canvas');
          canvas.width = canvas.height = 1;
          const context = canvas.getContext('2d')!;
          context.fillStyle = expectedStyle.backgroundColor;
          context.fillRect(0, 0, 1, 1);
          const surfaces: Element[] = [];
          for (let parent = element.parentElement; parent; parent = parent.parentElement) {
            surfaces.unshift(parent);
          }
          for (const surface of surfaces) {
            context.fillStyle = getComputedStyle(surface).backgroundColor;
            context.fillRect(0, 0, 1, 1);
          }
          const backgroundPixel = context.getImageData(0, 0, 1, 1).data;
          context.fillStyle = actualStyle.outlineColor;
          context.fillRect(0, 0, 1, 1);
          const outlinePixel = context.getImageData(0, 0, 1, 1).data;
          const luminance = (pixel: Uint8ClampedArray): number => {
            const [r, g, b] = Array.from(pixel).slice(0, 3).map((value) => {
              const channel = value / 255;
              return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
            });
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
          };
          const backgroundLuminance = luminance(backgroundPixel);
          const outlineLuminance = luminance(outlinePixel);
          const box = element.getBoundingClientRect();
          const outset = parseFloat(actualStyle.outlineWidth) + parseFloat(actualStyle.outlineOffset);
          const ring = { left: box.left - outset, right: box.right + outset,
            top: box.top - outset, bottom: box.bottom + outset };
          const clippedBy: string[] = [];
          if (ring.left < 0 || ring.top < 0 || ring.right > innerWidth || ring.bottom > innerHeight) {
            clippedBy.push('viewport');
          }
          for (const surface of surfaces) {
            const style = getComputedStyle(surface);
            const bounds = surface.getBoundingClientRect();
            const left = bounds.left + surface.clientLeft;
            const top = bounds.top + surface.clientTop;
            const paintContainment = /(?:paint|strict|content)/.test(style.contain);
            const clipsX = style.overflowX !== 'visible' || paintContainment;
            const clipsY = style.overflowY !== 'visible' || paintContainment;
            if ((clipsX && (ring.left < left || ring.right > left + surface.clientWidth))
              || (clipsY && (ring.top < top || ring.bottom > top + surface.clientHeight))) {
              clippedBy.push(surface.tagName);
            }
          }
          const observation = {
            focusVisible: element.matches(':focus-visible'),
            paintVisible: element.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true }),
            clippedBy,
            outlineStyle: actualStyle.outlineStyle,
            outlineWidth: actualStyle.outlineWidth,
            outlineOffset: actualStyle.outlineOffset,
            outlineColor: actualStyle.outlineColor,
            backgroundColor: actualStyle.backgroundColor,
            color: actualStyle.color,
            expectedWidth: expectedStyle.outlineWidth,
            expectedOffset: expectedStyle.outlineOffset,
            expectedColor: expectedStyle.outlineColor,
            expectedBackground: expectedStyle.backgroundColor,
            expectedText: expectedStyle.color,
            surroundingPixel: Array.from(backgroundPixel),
            outlinePixel: Array.from(outlinePixel),
            outlineContrast: (Math.max(backgroundLuminance, outlineLuminance) + 0.05)
              / (Math.min(backgroundLuminance, outlineLuminance) + 0.05),
            backgroundImages: surfaces.map((surface) => getComputedStyle(surface).backgroundImage)
              .filter((image) => image !== 'none'),
          };
          expected.remove();
          return observation;
        }, forcedColors === 'active');

        observations.push({ testId, ...focus });
      }
      const artifactPath = testInfo.outputPath('content-focus.json');
      await writeFile(artifactPath, `${JSON.stringify(observations, null, 2)}\n`);
      await testInfo.attach('content-focus.json', { path: artifactPath, contentType: 'application/json' });

      for (const focus of observations) {
        expect(focus.focusVisible).toBe(true);
        expect(focus.paintVisible).toBe(true);
        expect(focus.clippedBy, `${focus.testId} outline must fit outside its box without clipping`).toEqual([]);
        expect(focus.outlineStyle).toBe('solid');
        expect(parseFloat(focus.outlineWidth)).toBeGreaterThan(0);
        expect(parseFloat(focus.outlineOffset)).toBeGreaterThan(0);
        expect(focus.outlineWidth).toBe(focus.expectedWidth);
        expect(focus.outlineOffset).toBe(focus.expectedOffset);
        expect(focus.outlineColor).toBe(focus.expectedColor);
        expect(focus.outlineColor).not.toBe('rgba(0, 0, 0, 0)');
        expect(focus.outlineColor).not.toBe(focus.backgroundColor);
        expect(focus.backgroundImages, 'specimen backdrop must be solid for computed contrast proof').toEqual([]);
        expect(focus.outlineContrast, `${focus.testId} outline contrast against its surrounding surface`).toBeGreaterThanOrEqual(3);
        if (forcedColors === 'active') {
          expect(focus.backgroundColor).toBe(focus.expectedBackground);
          expect(focus.color).toBe(focus.expectedText);
        }
      }
    });
  }

  test('content reduced-motion rules suppress nonzero animation and transition settings', async ({ page }, testInfo) => {
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await gotoSpecimen(page);
    const controls = ['parties-specimen-primary-action', 'parties-specimen-link']
      .map((testId) => ({ testId, locator: page.getByTestId(testId) }));
    // Nonzero inline stimulus makes a missing or unscoped production override fail.
    // The test supplies no reduced-motion CSS or important declarations of its own.
    const ordinary: Awaited<ReturnType<typeof readMotionStyle>>[] = [];
    for (const control of controls) {
      await control.locator.evaluate((element) => {
        Object.assign(element.style, {
          animationDuration: '2s', animationIterationCount: 'infinite',
          transitionDuration: '3s', scrollBehavior: 'smooth',
        });
      });
      ordinary.push(await readMotionStyle(control.locator));
    }
    await page.emulateMedia({ reducedMotion: 'reduce' });
    try {
      await expect.poll(async () => Promise.all(controls.map(async (control) =>
        motionIsSuppressed(await readMotionStyle(control.locator)),
      ))).toEqual(controls.map(() => true));
    } finally {
      const observations = await Promise.all(controls.map(async (control, index) => ({
        testId: control.testId, ordinary: ordinary[index], reduced: await readMotionStyle(control.locator),
      })));
      const artifactPath = testInfo.outputPath('content-motion.json');
      await writeFile(artifactPath, `${JSON.stringify(observations, null, 2)}\n`);
      await testInfo.attach('content-motion.json', { path: artifactPath, contentType: 'application/json' });
    }
    for (const [index, control] of controls.entries()) {
      expect(ordinary[index]).toEqual({
        animationDuration: '2s', animationIterationCount: 'infinite',
        transitionDuration: '3s', scrollBehavior: 'smooth',
      });
      await expect(control.locator).toBeVisible();
      await expect(control.locator).not.toBeEmpty();
    }
  });

  test('filled primary button does not compute to raw teal', async ({ page }) => {
    await gotoSpecimen(page);

    const button = page.getByTestId('parties-specimen-primary-action');
    const backgroundColor = await button.evaluate((element) => getComputedStyle(element).backgroundColor);

    expect(backgroundColor).not.toBe('rgb(0, 151, 167)');
    await expect(button).toBeVisible();
  });

  test('semantic visual baseline for deterministic shell specimen', async ({ page }) => {
    await gotoSpecimen(page);
    const expected = JSON.parse(await readFile(VISUAL_BASELINE, 'utf8')) as PartiesVisualBaseline;
    expect(await collectVisualBaseline(page)).toEqual(expected);
  });

  test('visual baseline for deterministic shell specimen', async ({ page }, testInfo) => {
    test.skip(process.platform !== 'linux', 'The pixel baseline is qualified on Ubuntu 24.04 with locked Chromium.');
    await gotoSpecimen(page);
    await page.evaluate(() => document.fonts.ready);
    // The producer footer uses the server's current year. Normalize only that
    // dynamic value so year rollover cannot change the visual contract.
    await page.getByText(/^Hexalith FrontComposer © \d{4}$/).evaluate((element) => {
      element.textContent = element.textContent!.replace(/\d{4}$/, '2000');
    });

    const screenshot = await page.screenshot({
      fullPage: true,
      animations: 'disabled',
    });
    await testInfo.attach('parties-accessibility-shell.png', {
      body: screenshot,
      contentType: 'image/png',
    });
    await expect(page).toHaveScreenshot('parties-accessibility-shell.png', {
      fullPage: true,
      animations: 'disabled',
      caret: 'hide',
      maxDiffPixels: 0,
    });
  });
});

const auditSpecimen = async (page: import('@playwright/test').Page, artifactPath: string): Promise<void> =>
  expectNoBlockingAxeViolations(page, {
    route: SPECIMEN_ROUTE,
    requiredSelectors: [
      "[data-testid='parties-accessibility-specimen-ready']",
      '#fc-main-content',
      '#fc-nav',
      "a[href$='#fc-main-content']",
      "a[href$='#fc-nav']",
    ],
    artifactPath,
  });

const readMotionStyle = async (control: import('@playwright/test').Locator) => control.evaluate((element) => {
  const style = getComputedStyle(element);
  return {
    animationDuration: style.animationDuration,
    animationIterationCount: style.animationIterationCount,
    transitionDuration: style.transitionDuration,
    scrollBehavior: style.scrollBehavior,
  };
});

const motionIsSuppressed = (style: Awaited<ReturnType<typeof readMotionStyle>>): boolean => {
  const durationsAreShort = (value: string) => value.split(',').every((duration) => {
    const milliseconds = parseFloat(duration) * (duration.trim().endsWith('ms') ? 1 : 1_000);
    return Number.isFinite(milliseconds) && milliseconds >= 0 && milliseconds <= 10;
  });
  return durationsAreShort(style.animationDuration) && durationsAreShort(style.transitionDuration)
    && style.animationIterationCount.split(',').every((count) => Number(count) >= 0 && Number(count) <= 1)
    && style.scrollBehavior === 'auto';
};

const gotoSpecimen = async (page: import('@playwright/test').Page): Promise<void> => {
  await page.goto(SPECIMEN_ROUTE);
  await expect(page.getByTestId('parties-accessibility-specimen-ready'), `${SPECIMEN_ROUTE} missing ready marker`).toBeVisible();
  await expect(page.locator('.fc-shell-root[data-fc-interactive="true"]')).toBeVisible();
  await expect(page).toHaveTitle('Parties accessibility specimen');
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
  specimenSections: await page.locator('.parties-accessibility-specimen').getByRole('heading').evaluateAll((headings) =>
    headings.map((heading) => {
      const slot = heading.querySelector<HTMLSlotElement>('slot[name="heading"]');
      return (slot ? slot.assignedNodes({ flatten: true }).map(node => node.textContent).join('')
        : heading.textContent)?.trim() ?? '';
    })),
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
