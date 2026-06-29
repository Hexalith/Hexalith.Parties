import { expect, test, type BrowserContext, type Page } from '@playwright/test';

const ADMIN_COOKIE = 'parties-admin-e2e';
const CONSUMER_COOKIE = 'parties-consumer-e2e';
const RESET_ROUTE = '/__parties/specimens/admin-portal/reset';
const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:5072';

test.describe('Shared role and policy authorization', () => {
  test.use({
    locale: 'en-US',
    timezoneId: 'UTC',
  });

  test.beforeEach(async ({ page, request }) => {
    await request.post(RESET_ROUTE);
    await page.setViewportSize({ width: 1280, height: 900 });
  });

  test('Admin role lands in the Admin area and cannot cross-render Consumer profile data', async ({ context, page }) => {
    await enableAdminFixture(context, 'enabled');
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/');
    await page.waitForURL('**/admin/parties');

    await expect(page.getByRole('heading', { name: 'Parties' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'My profile' })).toHaveCount(0);
    await expect(page.getByText('Consumer E2E')).toHaveCount(0);

    await page.goto('/me');

    await expect(page.getByRole('heading', { name: 'Forbidden' })).toBeVisible();
    await expect(page.getByText('You are not authorized to view this area.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'My profile' })).toHaveCount(0);
    expect(browserVisibleDataRequests).toEqual([]);
  });

  for (const persona of [
    { cookieValue: 'tenant-owner', label: 'TenantOwner' },
    { cookieValue: 'tenantowner', label: 'tenantowner' },
  ] as const) {
    test(`${persona.label} role uses the shared Admin policy without changing the Admin surface`, async ({ context, page }) => {
      await enableAdminFixture(context, persona.cookieValue);
      const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

      await page.goto('/');
      await page.waitForURL('**/admin/parties');

      await expect(page.getByRole('heading', { name: 'Parties' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
      await expect(page.getByRole('heading', { name: 'My profile' })).toHaveCount(0);
      await expect(page.getByText('Consumer E2E')).toHaveCount(0);
      expect(browserVisibleDataRequests).toEqual([]);
    });
  }

  test('Consumer role lands in the Consumer area and is forbidden from the Admin policy', async ({ context, page }) => {
    await enableConsumerFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/');
    await page.waitForURL('**/me');

    await expect(page.getByRole('heading', { name: 'My profile' })).toBeVisible();
    await expect(page.getByText('Consumer E2E')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Parties' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toHaveCount(0);

    await page.goto('/admin/parties');

    await expect(page.getByRole('heading', { name: 'Forbidden' })).toBeVisible();
    await expect(page.getByText('An Admin role is required to view this area.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Parties' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Ada Lovelace' })).toHaveCount(0);
    expect(browserVisibleDataRequests).toEqual([]);
  });

  for (const route of ['/', '/admin/parties', '/me'] as const) {
    test(`unauthenticated ${route} challenges sign-in with the return URL preserved`, async ({ page }) => {
      const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

      await page.goto(route);
      await page.waitForURL((url) =>
        url.pathname === '/authentication/challenge'
        && url.searchParams.get('returnUrl') === route);

      const challengeUrl = new URL(page.url());
      expect(challengeUrl.pathname).toBe('/authentication/challenge');
      expect(challengeUrl.searchParams.get('returnUrl')).toBe(route);
      await expect(page.getByRole('heading', { name: 'Parties' })).toHaveCount(0);
      await expect(page.getByRole('heading', { name: 'My profile' })).toHaveCount(0);
      expect(browserVisibleDataRequests).toEqual([]);
    });
  }
});

const enableAdminFixture = async (
  context: BrowserContext,
  value: 'enabled' | 'tenant-owner' | 'tenantowner',
): Promise<void> => {
  await context.addCookies([
    {
      name: ADMIN_COOKIE,
      value,
      url: BASE_URL,
      sameSite: 'Lax',
    },
  ]);
};

const enableConsumerFixture = async (context: BrowserContext): Promise<void> => {
  await context.addCookies([
    {
      name: CONSUMER_COOKIE,
      value: 'bound',
      url: BASE_URL,
      sameSite: 'Lax',
    },
  ]);
};

const captureBrowserVisiblePartiesDataRequests = (page: Page): string[] => {
  const requests: string[] = [];
  page.on('request', (request) => {
    if (isBrowserVisiblePartiesDataRequest(request.url())) {
      requests.push(request.url());
    }
  });

  return requests;
};

const isBrowserVisiblePartiesDataRequest = (url: string): boolean => {
  const parsed = new URL(url);
  return parsed.pathname.includes('/api/v1/commands')
    || parsed.pathname.includes('/api/v1/queries');
};
