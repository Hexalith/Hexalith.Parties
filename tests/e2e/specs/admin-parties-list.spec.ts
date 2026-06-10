import { expect, test, type APIRequestContext, type BrowserContext, type Page } from '@playwright/test';

const ADMIN_ROUTE = '/admin/parties';
const ADMIN_COOKIE = 'parties-admin-e2e';
const REQUESTS_ROUTE = '/__parties/specimens/admin-portal/requests';
const RESET_ROUTE = '/__parties/specimens/admin-portal/reset';
const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:5072';

test.describe('Admin parties list', () => {
  test.describe.configure({ mode: 'serial' });

  test.use({
    locale: 'en-US',
    timezoneId: 'UTC',
  });

  test.beforeEach(async ({ context, page, request }) => {
    await request.post(RESET_ROUTE);
    await enableAdminFixture(context);
    await page.setViewportSize({ width: 1280, height: 900 });
  });

  test('debounced display-name search combines with type and active filters', async ({ page, request }) => {
    await gotoAdmin(page);

    await chooseOption(page, 'Party type', 'Person');
    await chooseOption(page, 'Active state', 'Active');
    await page.getByLabel('Search parties').fill('Ada');

    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
    await expect(page.getByText('Display-name search only')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Email' })).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Identifier' })).toBeDisabled();

    await expect.poll(async () => {
      const latest = await latestSearchRequest(request);
      return `${latest?.query}|${latest?.type}|${latest?.active}|${latest?.page}`;
    }).toBe('Ada|Person|true|1');
  });

  test('next page preserves the current search criteria', async ({ page, request }) => {
    await gotoAdmin(page);

    await chooseOption(page, 'Party type', 'Organization');
    await chooseOption(page, 'Active state', 'Active');
    await page.getByLabel('Search parties').fill('Paging');

    await expect(page.getByRole('button', { name: 'Paging Party 01' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Paging Party 21' })).toHaveCount(0);

    await page.getByRole('button', { name: 'Next' }).click();

    await expect(page.getByRole('button', { name: 'Paging Party 21' })).toBeVisible();
    await expect.poll(async () => {
      const latest = await latestSearchRequest(request);
      return `${latest?.query}|${latest?.type}|${latest?.active}|${latest?.page}`;
    }).toBe('Paging|Organization|true|2');
  });

  test('degraded search preserves last-known rows and announces stale data', async ({ page }) => {
    await gotoAdmin(page);

    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
    await page.getByLabel('Search parties').fill('stale');

    await expect(page.getByText('Data may be stale or degraded')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
  });

  test('empty search has a clear-filters recovery path', async ({ page }) => {
    await gotoAdmin(page);

    await page.getByLabel('Search parties').fill('NoMatch');

    await expect(page.getByText('No parties match.')).toBeVisible();
    await page.getByRole('button', { name: 'Clear', exact: true }).click();

    await expect(page.getByLabel('Search parties')).toHaveValue('');
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
  });

  test('keyboard navigation opens a row and type-ahead focuses search', async ({ page }) => {
    await gotoAdmin(page);

    const gridRegion = page.locator('.hx-parties-admin__list');
    await gridRegion.focus();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');

    await page.waitForURL('**/admin/parties/grace-hopper');
    await expect(page.getByRole('heading', { name: 'Grace Hopper' })).toBeVisible();

    await page.goto(ADMIN_ROUTE);
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
    await gridRegion.focus();
    await page.keyboard.press('a');

    await expect(page.getByLabel('Search parties')).toBeFocused();
  });

  test('desktop row click and direct route render the existing party detail surface', async ({ page, request }) => {
    await gotoAdmin(page);

    await page.getByRole('button', { name: 'Ada Lovelace' }).click();
    await page.waitForURL('**/admin/parties/ada-lovelace');

    const detail = page.getByLabel('Party detail');
    await expect(detail.getByRole('heading', { name: 'Ada Lovelace' })).toBeVisible();
    await expect(detail.locator('.party-state-badge', { hasText: 'Active' })).toBeVisible();
    await expect(detail.locator('.data-freshness-indicator [role="status"]')).toHaveText('Up to date');
    await expect(detail.getByRole('button', { name: 'Edit' })).toBeDisabled();
    await expect(detail.getByText('Edit is unavailable until party editing is enabled.')).toBeVisible();
    await expect(detail.getByText('GDPR operations')).toBeVisible();
    await expect.poll(async () => (await latestDetailRequest(request))?.partyId).toBe('ada-lovelace');

    await page.goto(`${ADMIN_ROUTE}/grace-hopper`);
    await expect(detail.getByRole('heading', { name: 'Grace Hopper' })).toBeVisible();
    await expect(detail.getByRole('heading', { name: 'Grace Hopper' })).toBeFocused();
    await expect.poll(async () => (await latestDetailRequest(request))?.partyId).toBe('grace-hopper');
  });

  test('phone detail behaves as a full-screen sheet and restores row focus on close', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 640 });
    await gotoAdmin(page);

    const row = page.getByRole('button', { name: 'Ada Lovelace' });
    await row.click();
    await page.waitForURL('**/admin/parties/ada-lovelace');

    const detail = page.getByLabel('Party detail');
    await expect(detail.getByRole('heading', { name: 'Ada Lovelace' })).toBeVisible();
    await expect(detail.getByRole('heading', { name: 'Ada Lovelace' })).toBeFocused();
    await expect(page.getByRole('button', { name: 'Grace Hopper' })).toHaveCount(0);
    await expect(detail.getByRole('button', { name: 'Back to list' })).toBeVisible();

    const horizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
    expect(horizontalOverflow).toBe(false);

    await detail.getByRole('button', { name: 'Back to list' }).click();
    await page.waitForURL('**/admin/parties');
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeFocused();
  });

  test('phone detail remains readable under zoom-equivalent narrow emulation', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 640 });
    await gotoAdmin(page);

    await page.getByRole('button', { name: 'Inactive Labs' }).click();
    await page.waitForURL('**/admin/parties/inactive-labs');

    await page.evaluate(() => {
      document.documentElement.style.zoom = '2';
    });

    const detail = page.getByLabel('Party detail');
    await expect(detail.getByRole('heading', { name: 'Inactive Labs' })).toBeVisible();
    await expect(detail.getByRole('button', { name: 'Back to list' })).toBeVisible();
    await expect(detail.locator('.party-state-badge', { hasText: 'Inactive' })).toBeVisible();

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
    expect(overflow).toBe(false);
  });

  test('phone direct route opens detail without an originating row and closes to search focus', async ({ page, request }) => {
    await page.setViewportSize({ width: 320, height: 640 });

    await page.goto(`${ADMIN_ROUTE}/grace-hopper`);

    const detail = page.getByLabel('Party detail');
    await expect(detail.getByRole('heading', { name: 'Grace Hopper' })).toBeVisible();
    await expect(detail.getByRole('heading', { name: 'Grace Hopper' })).toBeFocused();
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toHaveCount(0);
    await expect(detail.getByRole('button', { name: 'Back to list' })).toBeVisible();
    await expect.poll(async () => (await latestDetailRequest(request))?.partyId).toBe('grace-hopper');

    const horizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
    expect(horizontalOverflow).toBe(false);

    await detail.getByRole('button', { name: 'Back to list' }).click();
    await page.waitForURL('**/admin/parties');
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
    await expect(page.getByLabel('Search parties')).toBeFocused();
  });
});

const enableAdminFixture = async (context: BrowserContext): Promise<void> => {
  await context.addCookies([
    {
      name: ADMIN_COOKIE,
      value: 'enabled',
      url: BASE_URL,
      sameSite: 'Lax',
    },
  ]);
};

const gotoAdmin = async (page: Page): Promise<void> => {
  await page.goto(ADMIN_ROUTE);
  await expect(page.getByRole('heading', { name: 'Parties' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
};

const chooseOption = async (page: Page, label: string, option: string): Promise<void> => {
  await page.getByLabel(label).click();
  await page.getByRole('option', { name: option, exact: true }).click();
};

const latestSearchRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.searchRequests.at(-1);
};

const latestDetailRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.detailRequests.at(-1);
};

const requestSnapshot = async (request: APIRequestContext): Promise<AdminPortalE2eSnapshot> => {
  const response = await request.get(REQUESTS_ROUTE);
  expect(response.ok()).toBe(true);
  return await response.json() as AdminPortalE2eSnapshot;
};

interface AdminPortalE2eSnapshot {
  listRequests: AdminPortalRequestCapture[];
  searchRequests: AdminPortalRequestCapture[];
  detailRequests: AdminPortalRequestCapture[];
}

interface AdminPortalRequestCapture {
  kind: 'list' | 'search';
  query: string | null;
  page: number;
  pageSize: number;
  type: string | null;
  active: boolean | null;
  partyId: string | null;
}
