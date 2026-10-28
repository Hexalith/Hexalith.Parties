import { qualifyFrontComposerAssets } from '../helpers/frontcomposer-assets.js';
import { expect, test } from '@playwright/test';
import { expectConsumerNavigationCancelsDownload } from '../helpers/consumer-download-cancellation.js';

// Source qualification runs explicitly; the package CI portal suite stays unconditional.
test.describe('Mapped shared feedback source qualification', () => {
  test.use({ locale: 'en-US', timezoneId: 'UTC' });
  test.beforeEach(async ({ context, page, request }) => {
    await request.post('/__parties/specimens/admin-portal/reset');
    await page.setViewportSize({ width: 1280, height: 900 });
    await context.addCookies([{
      name: 'parties-consumer-e2e', value: 'bound',
      url: process.env.BASE_URL ?? 'http://127.0.0.1:5072', sameSite: 'Lax',
    }]);
  });

  test('Consumer navigation cancels an active shared download without late feedback or browser resources', async ({ page }) => {
    expect(await qualifyFrontComposerAssets(page), 'Run this source qualification with the mapped capability.').toBe('source');
    await expectConsumerNavigationCancelsDownload(page);
  });

});
