import { expect, type Page } from '@playwright/test';
import { portalAction } from './portal-actions.js';

/** Holds the browser stream-read seam while real page disposal cancels its registered service. */
export async function expectConsumerNavigationCancelsDownload(page: Page): Promise<void> {
  const downloads: string[] = [];
  const errors: string[] = [];
  page.on('download', download => downloads.push(download.suggestedFilename()));
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/me/privacy');
  await expect(page.getByRole('heading', { name: 'Data privacy', exact: true })).toBeFocused();
  await portalAction(page, 'Export my data').click();
  await expect(page.getByRole('button', { name: 'Download JSON' })).toBeVisible();
  await page.evaluate(() => {
    const probe = { entered: false, finished: false, pending: 0, clicks: 0, urls: 0,
      operationId: '', cancelledId: '', deletes: 0, release: () => {}, restore: () => {} };
    (window as unknown as { __consumerDownloadProbe: typeof probe }).__consumerDownloadProbe = probe;
    const read = Response.prototype.arrayBuffer;
    const set = Map.prototype.set;
    const remove = Map.prototype.delete;
    const click = HTMLAnchorElement.prototype.click;
    const create = URL.createObjectURL;
    // DotNetStreamReference reads a Response over its real browser stream. Holding
    // this native read leaves both the producer module and the .NET service intact.
    Response.prototype.arrayBuffer = async function () {
      const bytes = await read.call(this);
      if (probe.pending === 1 && !probe.entered) {
        probe.entered = true;
        await new Promise<void>(resolve => { probe.release = resolve; });
      }
      return bytes;
    };
    // Observe only the producer's operation entries; never replace its module/functions.
    Map.prototype.set = function (key: unknown, value: unknown) {
      if (typeof key === 'string' && /^[a-f0-9]{32}$/.test(key)
          && (value as { cancelled?: unknown } | null)?.cancelled === false) {
        probe.operationId = key;
        probe.pending = 1;
      }
      return set.call(this, key, value);
    };
    Map.prototype.delete = function (key: unknown) {
      if (probe.operationId && key === probe.operationId) {
        const operation = this.get(key) as { cancelled?: boolean } | undefined;
        if (operation?.cancelled) probe.cancelledId = key as string;
        probe.pending = 0;
        probe.deletes++;
        if (probe.deletes === 2) probe.finished = true;
      }
      return remove.call(this, key);
    };
    HTMLAnchorElement.prototype.click = function () { probe.clicks++; click.call(this); };
    URL.createObjectURL = blob => { probe.urls++; return create(blob); };
    probe.restore = () => {
      Response.prototype.arrayBuffer = read;
      Map.prototype.set = set;
      Map.prototype.delete = remove;
      HTMLAnchorElement.prototype.click = click;
      URL.createObjectURL = create;
    };
  });
  const probe = () => page.evaluate(() => {
    const value = (window as unknown as { __consumerDownloadProbe: {
      entered: boolean; finished: boolean; pending: number; clicks: number; urls: number;
      operationId: string; cancelledId: string; deletes: number;
    } }).__consumerDownloadProbe;
    return { entered: value.entered, finished: value.finished, pending: value.pending,
      clicks: value.clicks, urls: value.urls, operationId: value.operationId,
      cancelledId: value.cancelledId, deletes: value.deletes };
  });
  try {
    await page.getByRole('button', { name: 'Download JSON' }).click();
    await expect.poll(async () => (await probe()).entered).toBe(true);
    const active = await probe();
    expect(active.pending).toBe(1);
    expect(active.operationId).toMatch(/^[a-f0-9]{32}$/);
    await page.getByRole('link', { name: 'Manage all consent' }).click();
    await expect(page.getByRole('heading', { name: 'Consent', exact: true })).toBeVisible();
    await expect.poll(async () => (await probe()).cancelledId).toBe(active.operationId);
    expect(await probe()).toMatchObject({ pending: 0, finished: false, deletes: 1 });
    await page.evaluate(() => (window as unknown as { __consumerDownloadProbe: { release: () => void } }).__consumerDownloadProbe.release());
    await expect.poll(async () => (await probe()).finished).toBe(true);
    expect(await probe()).toMatchObject({ entered: true, pending: 0, clicks: 0, urls: 0, deletes: 2 });
    await expect(page.locator('a[download]')).toHaveCount(0);
    await expect(page.getByRole('alert')).toHaveCount(0);
    expect(downloads).toEqual([]);
    expect(errors).toEqual([]);
  } finally {
    await page.evaluate(() => {
      const probe = (window as unknown as { __consumerDownloadProbe: { release: () => void; restore: () => void } }).__consumerDownloadProbe;
      probe.release();
      probe.restore();
    });
  }
}
