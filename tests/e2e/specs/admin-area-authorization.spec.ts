import { expect, test } from '@playwright/test';

const ADMIN_ROUTES = [
  '/admin',
  '/admin/parties',
  '/admin/parties/party-123',
  '/admin/parties/party-123/gdpr',
];

test.describe('Admin area authorization', () => {
  test.use({
    locale: 'en-US',
    timezoneId: 'UTC',
  });

  test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 900 });
  });

  for (const route of ADMIN_ROUTES) {
    test(`unauthenticated ${route} challenges sign-in with the return URL preserved`, async ({ page }) => {
      const browserVisibleDataRequests: string[] = [];
      page.on('request', (request) => {
        if (isBrowserVisiblePartiesDataRequest(request.url())) {
          browserVisibleDataRequests.push(request.url());
        }
      });

      await page.goto(route);
      await page.waitForURL((url) =>
        url.pathname === '/authentication/challenge'
        && url.searchParams.get('returnUrl') === route);

      const challengeUrl = new URL(page.url());
      expect(challengeUrl.pathname).toBe('/authentication/challenge');
      expect(challengeUrl.searchParams.get('returnUrl')).toBe(route);

      await expect(page.getByRole('heading', { name: 'Parties' })).toHaveCount(0);
      await expect(page.getByText('Admin area is coming soon')).toHaveCount(0);
      await expect(page.getByText('Ada Lovelace')).toHaveCount(0);
      expect(browserVisibleDataRequests).toEqual([]);
    });
  }
});

const isBrowserVisiblePartiesDataRequest = (url: string): boolean => {
  const parsed = new URL(url);
  return parsed.pathname.includes('/api/v1/commands')
    || parsed.pathname.includes('/api/v1/queries');
};
