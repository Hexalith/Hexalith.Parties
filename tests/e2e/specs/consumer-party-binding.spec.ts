import { expect, test, type BrowserContext, type Page } from '@playwright/test';

const CONSUMER_COOKIE = 'parties-consumer-e2e';
const RESET_ROUTE = '/__parties/specimens/admin-portal/reset';
const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:5072';

test.describe('Consumer party binding landing', () => {
  test.use({
    locale: 'en-US',
    timezoneId: 'UTC',
  });

  test.beforeEach(async ({ page, request }) => {
    await request.post(RESET_ROUTE);
    await page.setViewportSize({ width: 1280, height: 900 });
  });

  test('bound Consumer reaches /me without browser-visible gateway calls', async ({ context, page }) => {
    await enableConsumerFixture(context, 'bound');
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/');
    await page.waitForURL('**/me');

    await expect(page.getByRole('heading', { name: 'My space' })).toBeVisible();
    await expect(page.getByText("Your account isn't linked to a profile yet.")).toHaveCount(0);
    expect(browserVisibleDataRequests).toEqual([]);
  });

  for (const state of ['unbound', 'empty', 'ambiguous', 'suspended', 'removed'] as const) {
    test(`${state} Consumer is sent to NoPartyBinding instead of /me`, async ({ context, page }) => {
      await enableConsumerFixture(context, state);
      const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

      await page.goto('/');
      await page.waitForURL('**/no-party-binding');

      await expect(page.getByRole('heading', { name: "We're still setting up your profile" })).toBeVisible();
      await expect(page.getByText("Your account isn't linked to a profile yet.")).toBeVisible();
      await expect(page.getByRole('heading', { name: 'My space' })).toHaveCount(0);
      expect(new URL(page.url()).pathname).toBe('/no-party-binding');
      expect(browserVisibleDataRequests).toEqual([]);
    });
  }
});

const enableConsumerFixture = async (
  context: BrowserContext,
  state: 'bound' | 'unbound' | 'empty' | 'ambiguous' | 'suspended' | 'removed',
): Promise<void> => {
  await context.addCookies([
    {
      name: CONSUMER_COOKIE,
      value: state,
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
