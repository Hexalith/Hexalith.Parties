import { expect, test, type APIRequestContext, type BrowserContext } from '@playwright/test';

const ADMIN_ROUTE = '/admin/parties';
const ADMIN_COOKIE = 'parties-admin-e2e';
const STORY_35_REAL_CONTRACT_COOKIE = 'parties-admin-e2e-story-3-5';
const REQUESTS_ROUTE = '/__parties/specimens/admin-portal/requests';
const RESET_ROUTE = '/__parties/specimens/admin-portal/reset';
const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:5072';

test.describe('Admin GDPR erasure verification contract', () => {
  test.use({
    locale: 'en-US',
    timezoneId: 'UTC',
  });

  test.beforeEach(async ({ context, page, request }) => {
    await request.post(RESET_ROUTE);
    await enableAdminFixture(context);
    await page.setViewportSize({ width: 1280, height: 900 });
  });

  test('real contract mode reads a bounded certificate and retries verification without browser-visible gateway calls', async ({ page, request }) => {
    await enableRealContractFixture(page.context());
    const browserVisibleDataRequests: string[] = [];
    page.on('request', routeRequest => {
      if (isBrowserVisiblePartiesDataRequest(routeRequest.url())) {
        browserVisibleDataRequests.push(routeRequest.url());
      }
    });

    await page.goto(`${ADMIN_ROUTE}/erasure-retry/gdpr`);

    const detail = page.getByLabel('Party detail');
    await expect(detail.getByRole('heading', { name: 'GDPR operations' })).toBeFocused();
    await expect(detail.getByText('Verification not yet available')).toBeVisible();
    await expect(detail.getByRole('button', { name: 'Retry verification' })).toHaveCount(0);

    await detail.getByRole('button', { name: 'Refresh erasure status' }).click();

    await expect(detail.getByText('VerificationFailed')).toBeVisible();
    await expect(detail.getByRole('button', { name: 'Retry verification' })).toBeVisible();
    await expect.poll(async () => (await gdprRequestCounts(request)).erasureCertificate).toBe(0);

    await detail.getByRole('button', { name: 'Retry verification' }).click();

    await expect(detail.getByRole('status').filter({ hasText: 'Operation completed' })).toBeVisible();
    await expect(detail.getByText('Correlation id: corr-retry-e2e')).toBeVisible();
    const report = detail.locator('section[aria-labelledby^="erasure-report-heading-"]');
    await expect(report.getByText('Verification confirmed')).toBeVisible();
    await expect(report.getByText('Verification status')).toBeVisible();
    await expect(report.getByText('Verified', { exact: true })).toBeVisible();
    await expect(report.getByText('Report status')).toBeVisible();
    await expect(report.getByText('Complete', { exact: true })).toBeVisible();
    await expect(report.getByText('Verified across projections')).toBeVisible();
    await expect(report.getByText('Key versions destroyed')).toBeVisible();
    await expect.poll(async () => (await latestRetryVerificationRequest(request))?.partyId).toBe('erasure-retry');
    await expect.poll(async () => (await latestErasureCertificateRequest(request))?.partyId).toBe('erasure-retry');
    await expect.poll(async () => (await gdprRequestCounts(request)).retryVerification).toBe(1);
    await expect.poll(async () => (await gdprRequestCounts(request)).erasureCertificate).toBe(1);

    await expect(detail.getByRole('button', { name: 'Retry verification' })).toHaveCount(0);
    await expect(detail.getByText('destroyed-key', { exact: false })).toHaveCount(0);
    await expect(detail.getByText('stateKey', { exact: false })).toHaveCount(0);
    await expect(detail.getByText('ProblemDetails', { exact: false })).toHaveCount(0);
    await expect(detail.getByText('erasure-retry', { exact: false })).toHaveCount(0);
    expect(JSON.stringify(await requestSnapshot(request))).not.toContain('key-material');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('provisional contract mode shows bounded fallback without blocking the GDPR page', async ({ page, request }) => {
    const browserVisibleDataRequests: string[] = [];
    page.on('request', routeRequest => {
      if (isBrowserVisiblePartiesDataRequest(routeRequest.url())) {
        browserVisibleDataRequests.push(routeRequest.url());
      }
    });
    await page.setViewportSize({ width: 390, height: 844 });

    await page.goto(`${ADMIN_ROUTE}/erasure-retry/gdpr`);

    const detail = page.getByLabel('Party detail');
    await expect(detail.getByRole('heading', { name: 'GDPR operations' })).toBeFocused();
    const report = detail.locator('section[aria-labelledby^="erasure-report-heading-"]');
    await expect(report.getByRole('status')).toContainText('Verification not yet available');
    await expect(report.getByText('The erasure status remains available')).toBeVisible();
    await expect(detail.getByText('EventStore GDPR client contract does not expose erasure certificate or retry verification yet.')).toBeVisible();
    await expect(detail.getByRole('button', { name: 'Refresh erasure status' })).toBeEnabled();
    await expect(detail.getByRole('button', { name: 'Request erasure' })).toBeEnabled();
    await expect(detail.getByRole('button', { name: 'Processing records' })).toBeEnabled();
    await expect(detail.getByRole('button', { name: 'Retry verification' })).toHaveCount(0);

    await detail.getByRole('button', { name: 'Refresh erasure status' }).click();

    await expect(report.getByRole('status')).toContainText('Verification not yet available');
    await expect.poll(async () => (await gdprRequestCounts(request)).erasureCertificate).toBe(0);
    await expect.poll(async () => (await gdprRequestCounts(request)).retryVerification).toBe(0);
    await expect(detail.getByText('ContractUnavailable', { exact: false })).toHaveCount(0);
    await expect(detail.getByText('ProblemDetails', { exact: false })).toHaveCount(0);
    await expect(detail.getByText('destroyed-key', { exact: false })).toHaveCount(0);
    await expect(detail.getByText('erasure-retry', { exact: false })).toHaveCount(0);
    expect(browserVisibleDataRequests).toEqual([]);
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

const enableRealContractFixture = async (context: BrowserContext): Promise<void> => {
  await context.addCookies([
    {
      name: STORY_35_REAL_CONTRACT_COOKIE,
      value: 'enabled',
      url: BASE_URL,
      sameSite: 'Lax',
    },
  ]);
};

const latestErasureCertificateRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.erasureCertificateRequests.at(-1);
};

const latestRetryVerificationRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.retryVerificationRequests.at(-1);
};

const requestSnapshot = async (request: APIRequestContext): Promise<AdminPortalE2eSnapshot> => {
  const response = await request.get(REQUESTS_ROUTE);
  expect(response.ok()).toBe(true);
  return await response.json() as AdminPortalE2eSnapshot;
};

const gdprRequestCounts = async (request: APIRequestContext): Promise<GdprRequestCounts> => {
  const snapshot = await requestSnapshot(request);
  return {
    erasureCertificate: snapshot.erasureCertificateRequests.length,
    retryVerification: snapshot.retryVerificationRequests.length,
  };
};

const isBrowserVisiblePartiesDataRequest = (url: string): boolean => {
  const parsed = new URL(url);
  return parsed.pathname.includes('/api/v1/commands')
    || parsed.pathname.includes('/api/v1/queries');
};

interface AdminPortalE2eSnapshot {
  erasureCertificateRequests: AdminPortalRequestCapture[];
  retryVerificationRequests: AdminPortalRequestCapture[];
}

interface AdminPortalRequestCapture {
  kind: 'erasure-certificate' | 'retry-verification';
  partyId: string | null;
}

interface GdprRequestCounts {
  erasureCertificate: number;
  retryVerification: number;
}
