import { expect, test, type BrowserContext, type Page } from '@playwright/test';

const CONSUMER_COOKIE = 'parties-consumer-e2e';
const RESET_ROUTE = '/__parties/specimens/admin-portal/reset';
const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:5072';

const CONSUMER_ROUTES = [
  { path: '/me', heading: 'My profile', status: 'Up to date' },
  { path: '/me/edit', heading: 'Edit profile', status: 'Profile updates are not submitted in this setup screen.' },
  { path: '/me/consent', heading: 'Consent', status: 'Consent choices are not changed in this setup screen.' },
  { path: '/me/privacy', heading: 'Data privacy', status: 'Privacy requests are not started in this setup screen.' },
] as const;

test.describe('Consumer portal route shells', () => {
  test.use({
    locale: 'en-US',
    timezoneId: 'UTC',
  });

  test.beforeEach(async ({ page, request }) => {
    await request.post(RESET_ROUTE);
    await page.setViewportSize({ width: 1280, height: 900 });
  });

  for (const route of CONSUMER_ROUTES) {
    test(`bound Consumer can open ${route.path} without browser-visible gateway calls`, async ({ context, page }) => {
      await enableConsumerFixture(context);
      const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

      await page.goto(route.path);

      await expect(page.getByRole('heading', { name: route.heading })).toBeVisible();
      await expect(page.getByRole('status')).toContainText(route.status);
      if (route.path === '/me') {
        await expect(page.getByText('Consumer E2E')).toBeVisible();
        await expect(page.getByRole('heading', { name: 'Profile details' })).toBeVisible();
      } else {
        await expect(page.getByRole('heading', { name: 'What will be available here' })).toBeVisible();
      }
      expect(new URL(page.url()).pathname).toBe(route.path);
      expect(browserVisibleDataRequests).toEqual([]);
    });
  }

  for (const route of CONSUMER_ROUTES) {
    test(`unauthenticated ${route.path} challenges sign-in with the return URL preserved`, async ({ page }) => {
      const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

      await page.goto(route.path);
      await page.waitForURL((url) =>
        url.pathname === '/authentication/challenge'
        && url.searchParams.get('returnUrl') === route.path);

      const challengeUrl = new URL(page.url());
      expect(challengeUrl.pathname).toBe('/authentication/challenge');
      expect(challengeUrl.searchParams.get('returnUrl')).toBe(route.path);
      await expect(page.getByRole('heading', { name: route.heading })).toHaveCount(0);
      expect(browserVisibleDataRequests).toEqual([]);
    });
  }
});

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
