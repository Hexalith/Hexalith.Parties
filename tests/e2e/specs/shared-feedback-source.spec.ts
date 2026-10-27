import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { portalAction } from '../helpers/portal-actions.js';

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
    const configuration = process.env.CI ? 'Release' : 'Debug';
    const [fromSource, mappingMode] = (await readFile(new URL(
      `../../../src/Hexalith.Parties.UI/bin/${configuration}/net10.0/Hexalith.Parties.UI.source-metadata.txt`, import.meta.url), 'utf8'))
      .trim().split(/\r?\n/);
    expect(fromSource, 'Run this source qualification with the mapped FrontComposer capability.').toBe('true');
    expect(mappingMode).toBe('source');
    const downloads: string[] = [];
    const errors: string[] = [];
    page.on('download', download => downloads.push(download.suggestedFilename()));
    page.on('pageerror', error => errors.push(error.message));
    await page.route('**/_content/Hexalith.FrontComposer.Shell/js/fc-download.js', async route => {
      const response = await route.fetch();
      const source = await response.text();
      const owner = await readFile(new URL('../../../references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.Shell/wwwroot/js/fc-download.js', import.meta.url));
      expect(createHash('sha256').update(source).digest('hex')).toBe(createHash('sha256').update(owner).digest('hex'));
      // Keep the real transfer and cancellation code. A test-only wrapper holds its
      // actual stream read pending and exposes the operation count for cleanup proof.
      const body = source.replace('export async function downloadStream(', 'async function originalDownloadStream(') + `
export async function downloadStream(...args) {
  const probe = window.__consumerDownloadProbe;
  Object.defineProperty(probe, 'pending', { get: () => pending.size });
  const read = args[3].arrayBuffer.bind(args[3]);
  args[3].arrayBuffer = async () => {
    const bytes = await read();
    probe.entered = true;
    await new Promise(resolve => { probe.release = resolve; });
    return bytes;
  };
  try { await originalDownloadStream(...args); }
  finally { probe.finished = true; }
}
`;
      await route.fulfill({ response, body });
    });
    await page.goto('/me/privacy');
    await portalAction(page, 'Export my data').click();
    await expect(page.getByRole('button', { name: 'Download JSON' })).toBeVisible();
    await page.evaluate(() => {
      const probe = { entered: false, finished: false, clicks: 0, urls: 0, release: () => {} };
      (window as unknown as { __consumerDownloadProbe: typeof probe }).__consumerDownloadProbe = probe;
      const click = HTMLAnchorElement.prototype.click;
      const create = URL.createObjectURL;
      HTMLAnchorElement.prototype.click = function () { probe.clicks++; click.call(this); };
      URL.createObjectURL = blob => { probe.urls++; return create(blob); };
    });
    const probe = () => page.evaluate(() => {
      const value = (window as unknown as { __consumerDownloadProbe: {
        entered: boolean; finished: boolean; clicks: number; urls: number; pending: number;
      } }).__consumerDownloadProbe;
      return { entered: value.entered, finished: value.finished, clicks: value.clicks, urls: value.urls, pending: value.pending };
    });
    await page.getByRole('button', { name: 'Download JSON' }).click();
    await expect.poll(async () => (await probe()).entered).toBe(true);
    expect((await probe()).pending).toBe(1);
    await page.getByRole('link', { name: 'Manage all consent' }).click();
    await expect(page.getByRole('heading', { name: 'Consent', exact: true })).toBeVisible();
    // The read is still held: only the adapter's cancellation can remove this operation.
    await expect.poll(async () => (await probe()).pending).toBe(0);
    expect((await probe()).finished).toBe(false);
    await page.evaluate(() => (window as unknown as { __consumerDownloadProbe: { release: () => void } }).__consumerDownloadProbe.release());
    await expect.poll(async () => (await probe()).finished).toBe(true);
    expect(await probe()).toEqual({ entered: true, finished: true, clicks: 0, urls: 0, pending: 0 });
    await expect(page.locator('a[download]')).toHaveCount(0);
    await expect(page.getByRole('alert')).toHaveCount(0);
    expect(downloads).toEqual([]);
    expect(errors).toEqual([]);
  });

});
