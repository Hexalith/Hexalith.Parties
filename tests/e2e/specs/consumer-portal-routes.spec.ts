import { expect, test, type APIRequestContext, type BrowserContext, type Locator, type Page } from '@playwright/test';

const ADMIN_COOKIE = 'parties-admin-e2e';
const CONSUMER_COOKIE = 'parties-consumer-e2e';
const CONSUMER_ERASURE_STATUS_COOKIE = 'parties-consumer-erasure-e2e';
const CONSUMER_PROCESSING_STATE_COOKIE = 'parties-consumer-processing-e2e';
const REQUESTS_ROUTE = '/__parties/specimens/admin-portal/requests';
const RESET_ROUTE = '/__parties/specimens/admin-portal/reset';
const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:5072';

const CONSUMER_ROUTES = [
  { path: '/me', heading: 'My profile', status: 'Up to date' },
  { path: '/me/edit', heading: 'Edit profile', status: 'Up to date' },
  { path: '/me/consent', heading: 'Consent', status: 'Up to date' },
  { path: '/me/privacy', heading: 'Data privacy', status: 'Ready to prepare your export.' },
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
      await expect(page.getByRole('status').filter({ hasText: route.status })).toBeVisible();
      if (route.path === '/me') {
        await expect(page.getByText('Consumer E2E')).toBeVisible();
        await expect(page.getByRole('heading', { name: 'Profile details' })).toBeVisible();
      } else if (route.path === '/me/edit') {
        await expect(page.getByText('Consumer E2E')).toBeVisible();
        await expect(page.getByRole('heading', { name: 'Editable details' })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Save changes' })).toBeVisible();
      } else if (route.path === '/me/consent') {
        await expect(page.getByRole('heading', { name: 'Things you control' })).toBeVisible();
        await expect(page.getByRole('heading', { name: 'Things we keep to run your account' })).toBeVisible();
        await expect(page.getByRole('switch', { name: 'Marketing emails' })).toHaveAttribute('aria-checked', 'false');
        await expect(page.getByRole('button', { name: 'Object (Art. 21)' })).toBeVisible();
      } else {
        await expect(page.getByRole('heading', { name: 'Export my data' })).toBeVisible();
        await expect(page.getByRole('heading', { name: 'What we process about you' })).toBeVisible();
        await expect(page.getByRole('heading', { name: 'Delete my data' })).toBeVisible();
        await expect(page.getByText('right to erasure')).toBeVisible();
        await expect(page.getByText('Machine-readable JSON')).toBeVisible();
        await expect(page.getByRole('link', { name: 'Manage all consent' })).toHaveAttribute('href', '/me/consent');
        await expect(page.getByRole('button', { name: 'Export my data' })).toBeVisible();
        await expect(page.getByRole('button', { name: 'Delete my data' })).toBeVisible();
        await expect(page.getByText('under one minute')).toHaveCount(0);
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

  test('Admin-only user cannot open /me/consent or see Consumer consent data', async ({ context, page, request }) => {
    await enableAdminFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/consent');

    await expect(page.getByRole('heading', { name: 'Forbidden' })).toBeVisible();
    await expect(page.getByText('You are not authorized to view this area.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Consent' })).toHaveCount(0);
    await expect(page.getByRole('switch')).toHaveCount(0);
    await expect.poll(async () => (await gdprRequestCounts(request)).addConsent).toBe(0);
    await expect.poll(async () => (await gdprRequestCounts(request)).revokeConsent).toBe(0);
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('Admin-only user cannot open /me/privacy or see Consumer export data', async ({ context, page, request }) => {
    await enableAdminFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/privacy');

    await expect(page.getByRole('heading', { name: 'Forbidden' })).toBeVisible();
    await expect(page.getByText('You are not authorized to view this area.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Data privacy' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Export my data' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Delete my data' })).toHaveCount(0);
    await expect.poll(async () => (await gdprRequestCounts(request)).exportData).toBe(0);
    await expect.poll(async () => (await gdprRequestCounts(request)).erasure).toBe(0);
    await expect.poll(async () => (await gdprRequestCounts(request)).cancelErasure).toBe(0);
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer saves editable profile details through the self-scoped command path', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/edit');
    await expect(page.getByRole('heading', { name: 'Edit profile' })).toBeVisible();
    await expect(page.getByLabel('First name')).toHaveValue('Consumer');
    await expect(page.getByLabel('Last name')).toHaveValue('Consumer');

    await page.getByLabel('First name').fill('Updated');
    await page.getByRole('button', { name: 'Save changes' }).click();

    await expect.poll(async () => (await latestUpdateRequest(request))?.partyId ?? null).toBe('party-bound-001');
    await expect(page.getByRole('status')).toHaveCount(1);
    await expect(page.getByRole('status')).toContainText('Saved');
    await expect(page.getByLabel('First name')).toHaveValue('Updated');
    await expect(page.getByText('Updated Consumer')).toBeVisible();
    expect(new URL(page.url()).pathname).toBe('/me/edit');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer validation preserves typed values and does not issue a command', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/edit');
    await expect(page.getByRole('heading', { name: 'Edit profile' })).toBeVisible();
    await page.getByLabel('First name').fill('Typed');
    await page.getByLabel('Last name').fill('');

    await page.getByRole('button', { name: 'Save changes' }).click();

    await expect(page.getByRole('alert').filter({ hasText: 'Check the highlighted fields and try again.' })).toBeVisible();
    await expect(page.getByLabel('First name')).toHaveValue('Typed');
    await expect(page.getByLabel('Last name')).toHaveValue('');
    expect(await latestUpdateRequest(request)).toBeUndefined();
    await expect(page.getByText('Saving...')).toHaveCount(0);
    await expect(page.getByText('Saved - updating')).toHaveCount(0);
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer consent surface exposes semantic switches and honest lawful-basis split', async ({ context, page }) => {
    await enableConsumerFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/consent');

    await expect(page.getByRole('heading', { name: 'Things you control' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Things we keep to run your account' })).toBeVisible();
    await expect(page.getByRole('switch')).toHaveCount(2);
    await expectConsentSwitchDescription(page.getByRole('switch', { name: 'Marketing emails' }), [
      'Messages about offers and campaigns.',
      'Lawful basis: consent.',
    ]);
    await expectConsentSwitchDescription(page.getByRole('switch', { name: 'Product updates' }), [
      'Messages about new or changed product features.',
      'Lawful basis: consent.',
    ]);
    await expect(page.getByText('Lawful basis: contract.')).toBeVisible();
    await expect(page.getByText('Lawful basis: legal obligation.')).toBeVisible();
    await expect(page.getByText('Lawful basis: legitimate interest.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Object (Art. 21)' })).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Withdraw consent' })).toHaveCount(0);
    await expect(page.getByText('consumer@example.test')).toHaveCount(0);
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer grants and withdraws consent through the self-scoped command path', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/consent');
    const marketing = page.getByRole('switch', { name: 'Marketing emails' });
    await expect(marketing).toHaveAttribute('aria-checked', 'false');

    await marketing.click();

    await expect.poll(async () => (await latestAddConsentRequest(request))?.partyId ?? null).toBe('party-bound-001');
    await expect(page.getByRole('status')).toHaveCount(1);
    await expect(page.getByRole('status')).toContainText('Saved');
    await expect(marketing).toHaveAttribute('aria-checked', 'true');
    await expect(page.getByText('consumer@example.test')).toHaveCount(0);

    await marketing.click();

    await expect.poll(async () => (await latestRevokeConsentRequest(request))?.partyId ?? null).toBe('party-bound-001');
    await expect(page.getByRole('status')).toHaveCount(1);
    await expect(page.getByRole('status')).toContainText('Saved');
    await expect(marketing).toHaveAttribute('aria-checked', 'false');
    await expect.poll(async () => await gdprRequestCounts(request)).toEqual({
      addConsent: 1,
      revokeConsent: 1,
      exportData: 0,
      erasure: 0,
      cancelErasure: 0,
    });
    expect(new URL(page.url()).pathname).toBe('/me/consent');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer exports own data through the self-scoped path', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/privacy');
    await page.evaluate(() => {
      window.HexalithPartiesConsumerPortal = {
        downloadJson: async (fileName: string, contentType: string) => {
          window.__consumerPrivacyExportDownload = { fileName, contentType };
        },
      };
    });

    await expect(page.getByRole('heading', { name: 'Export my data' })).toBeVisible();
    await expect(page.getByText('Machine-readable JSON')).toBeVisible();
    await page.getByRole('button', { name: 'Export my data' }).click();

    await expect(page.getByRole('status')).toContainText('Your JSON export is ready.');
    await expect.poll(async () => (await latestExportRequest(request))?.partyId ?? null).toBe('party-bound-001');
    await expect(page.getByText('Preparing your export - this can take a little while.')).toHaveCount(0);
    await expect(page.getByText('under one minute')).toHaveCount(0);
    await expect(page.getByText('party-bound-001')).toHaveCount(0);
    await page.getByRole('button', { name: 'Download JSON' }).click();
    await expect.poll(async () => await page.evaluate(() => window.__consumerPrivacyExportDownload?.contentType ?? null))
      .toBe('application/json');
    await expect.poll(async () => await page.evaluate(() => window.__consumerPrivacyExportDownload?.fileName ?? null))
      .toContain('my-data-export-');
    expect(new URL(page.url()).pathname).toBe('/me/privacy');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer sees processing summary through the self-scoped path', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/privacy');

    await expect(page.getByRole('heading', { name: 'What we process about you' })).toBeVisible();
    await expect(page.getByText('Bounded GDPR operation record')).toBeVisible();
    await expect(page.getByText('Data read')).toBeVisible();
    await expect(page.getByText('Completed')).toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: 'Processing summary is current.' })).toBeVisible();
    await expect.poll(async () => (await latestProcessingRecordRequest(request))?.partyId ?? null).toBe('party-bound-001');
    await expect(page.getByText('party-bound-001')).toHaveCount(0);
    await expect(page.getByText('test-tenant')).toHaveCount(0);
    await expect(page.getByText('admin-e2e')).toHaveCount(0);
    await expect(page.getByText('corr-gdpr-e2e')).toHaveCount(0);

    await page.getByRole('link', { name: 'Manage all consent' }).click();

    await expect(page.getByRole('heading', { name: 'Consent' })).toBeVisible();
    expect(new URL(page.url()).pathname).toBe('/me/consent');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer sees bounded empty processing state with privacy actions still usable', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    await enableConsumerProcessingStateFixture(context, 'empty');
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/privacy');

    await expect(page.getByRole('heading', { name: 'What we process about you' })).toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: 'No processing records are available to show right now.' })).toBeVisible();
    await expect(page.getByText('Bounded GDPR operation record')).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Export my data' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Delete my data' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Export my data' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Delete my data' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Manage all consent' })).toHaveAttribute('href', '/me/consent');
    await expect.poll(async () => (await latestProcessingRecordRequest(request))?.partyId ?? null).toBe('party-bound-001');
    await expect(page.getByText('party-bound-001')).toHaveCount(0);
    await expect(page.getByText('test-tenant')).toHaveCount(0);
    expect(new URL(page.url()).pathname).toBe('/me/privacy');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer sees bounded processing retry guidance after transient load failure', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    await enableConsumerProcessingStateFixture(context, 'transient-failure');
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/privacy');

    await expect(page.getByRole('heading', { name: 'What we process about you' })).toBeVisible();
    await expect(page.getByRole('alert')).toContainText('Processing summary is unavailable.');
    await expect(page.getByRole('alert')).toContainText('Try again in a moment. Export and deletion tools remain available where possible.');
    await expect(page.getByRole('heading', { name: 'Export my data' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Delete my data' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Export my data' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Delete my data' })).toBeVisible();
    await expect.poll(async () => (await latestProcessingRecordRequest(request))?.partyId ?? null).toBe('party-bound-001');
    await expect(page.getByText('Processing records fixture timeout.')).toHaveCount(0);
    await expect(page.getByText('party-bound-001')).toHaveCount(0);
    await expect(page.getByText('test-tenant')).toHaveCount(0);
    await expect(page.getByText('admin-e2e')).toHaveCount(0);
    await expect(page.getByText('corr-gdpr-e2e')).toHaveCount(0);
    expect(new URL(page.url()).pathname).toBe('/me/privacy');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer requests and cancels erasure through the self-scoped path', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/privacy');
    await expect(page.getByRole('heading', { name: 'Delete my data' })).toBeVisible();
    await expect(page.getByText('You can cancel until deletion begins')).toBeVisible();
    await expect(page.getByText("Once it's done, it's permanent - we can't undo it.")).toBeVisible();

    await page.getByRole('button', { name: 'Delete my data' }).click();
    await expect(page.getByRole('dialog', { name: 'Confirm deletion request' })).toBeVisible();
    await expect(page.getByRole('dialog')).toContainText('You can cancel until deletion begins');
    await expect(page.getByRole('textbox')).toHaveCount(0);

    await page.getByRole('button', { name: 'Confirm delete my data' }).click();

    await expect.poll(async () => (await latestErasureRequest(request))?.partyId ?? null).toBe('party-bound-001');
    await expect(page.getByRole('status').filter({ hasText: 'Deletion requested.' })).toContainText('You can cancel until deletion begins');
    await expect(page.getByRole('button', { name: 'Cancel deletion request' })).toBeVisible();

    await page.getByRole('button', { name: 'Cancel deletion request' }).click();

    await expect.poll(async () => (await latestCancelErasureRequest(request))?.partyId ?? null).toBe('party-bound-001');
    await expect(page.getByText('No deletion request is active.')).toBeVisible();
    await expect(page.getByText('within 30 days')).toHaveCount(0);
    await expect(page.getByText('successfully deleted')).toHaveCount(0);
    expect(new URL(page.url()).pathname).toBe('/me/privacy');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer cannot cancel erasure once deletion has begun', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    await enableConsumerErasureStatusFixture(context, 'key-destroyed');
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/privacy');

    await expect(page.getByRole('heading', { name: 'Export my data' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Delete my data' })).toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: 'Deletion has begun.' })).toContainText(
      'Cancellation is no longer available.',
    );
    await expect(page.getByRole('button', { name: 'Cancel deletion request' })).toBeDisabled();
    await expect(page.getByText('Cancellation is unavailable after deletion begins.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Delete my data' })).toHaveCount(0);
    await expect(page.getByText('within 30 days')).toHaveCount(0);
    await expect(page.getByText('successfully deleted')).toHaveCount(0);
    await expect(page.getByText('party-bound-001')).toHaveCount(0);
    await expect.poll(async () => await gdprRequestCounts(request)).toEqual({
      addConsent: 0,
      revokeConsent: 0,
      exportData: 0,
      erasure: 0,
      cancelErasure: 0,
    });
    expect(new URL(page.url()).pathname).toBe('/me/privacy');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer sees permanent erasure copy without a cancel action after deletion completes', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    await enableConsumerErasureStatusFixture(context, 'erased');
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/privacy');

    await expect(page.getByRole('heading', { name: 'Export my data' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Delete my data' })).toBeVisible();
    await expect(page.getByRole('status')).toContainText("Once it's done, it's permanent - we can't undo it.");
    await expect(page.getByRole('button', { name: 'Cancel deletion request' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Delete my data' })).toHaveCount(0);
    await expect(page.getByText('reversible')).toHaveCount(0);
    await expect(page.getByText('successfully deleted')).toHaveCount(0);
    await expect(page.getByText('party-bound-001')).toHaveCount(0);
    await expect.poll(async () => await gdprRequestCounts(request)).toEqual({
      addConsent: 0,
      revokeConsent: 0,
      exportData: 0,
      erasure: 0,
      cancelErasure: 0,
    });
    expect(new URL(page.url()).pathname).toBe('/me/privacy');
    expect(browserVisibleDataRequests).toEqual([]);
  });

  test('bound Consumer sees retry guidance when the JSON download helper fails', async ({ context, page, request }) => {
    await enableConsumerFixture(context);
    const browserVisibleDataRequests = captureBrowserVisiblePartiesDataRequests(page);

    await page.goto('/me/privacy');
    await page.evaluate(() => {
      window.HexalithPartiesConsumerPortal = {
        downloadJson: async () => {
          throw new Error('download unavailable');
        },
      };
    });

    await page.getByRole('button', { name: 'Export my data' }).click();
    await expect(page.getByRole('status')).toContainText('Your JSON export is ready.');
    await expect.poll(async () => (await latestExportRequest(request))?.partyId ?? null).toBe('party-bound-001');

    await page.getByRole('button', { name: 'Download JSON' }).click();

    await expect(page.getByRole('alert')).toHaveCount(1);
    await expect(page.getByRole('alert')).toContainText('Your data is safe - try again.');
    await expect(page.getByText('Wait a short time, then retry the export.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Download JSON' })).toBeVisible();
    await expect(page.getByText('completed')).toHaveCount(0);
    await expect.poll(async () => (await gdprRequestCounts(request)).exportData).toBe(1);
    expect(new URL(page.url()).pathname).toBe('/me/privacy');
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

const enableConsumerErasureStatusFixture = async (
  context: BrowserContext,
  status: 'pending' | 'key-destroyed' | 'verification-in-progress' | 'verified' | 'erased',
): Promise<void> => {
  await context.addCookies([
    {
      name: CONSUMER_ERASURE_STATUS_COOKIE,
      value: status,
      url: BASE_URL,
      sameSite: 'Lax',
    },
  ]);
};

const enableConsumerProcessingStateFixture = async (
  context: BrowserContext,
  status: 'empty' | 'transient-failure',
): Promise<void> => {
  await context.addCookies([
    {
      name: CONSUMER_PROCESSING_STATE_COOKIE,
      value: status,
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

const latestUpdateRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.updateRequests.at(-1);
};

const latestAddConsentRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.addConsentRequests.at(-1);
};

const latestRevokeConsentRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.revokeConsentRequests.at(-1);
};

const latestExportRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.exportRequests.at(-1);
};

const latestProcessingRecordRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.processingRecordRequests.at(-1);
};

const latestErasureRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.erasureRequests.at(-1);
};

const latestCancelErasureRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.cancelErasureRequests.at(-1);
};

const gdprRequestCounts = async (request: APIRequestContext): Promise<GdprRequestCounts> => {
  const snapshot = await requestSnapshot(request);
  return {
    addConsent: snapshot.addConsentRequests.length,
    revokeConsent: snapshot.revokeConsentRequests.length,
    exportData: snapshot.exportRequests.length,
    erasure: snapshot.erasureRequests.length,
    cancelErasure: snapshot.cancelErasureRequests.length,
  };
};

const requestSnapshot = async (request: APIRequestContext): Promise<AdminPortalE2eSnapshot> => {
  const response = await request.get(REQUESTS_ROUTE);
  expect(response.ok()).toBe(true);
  return await response.json() as AdminPortalE2eSnapshot;
};

const expectConsentSwitchDescription = async (control: Locator, expectedTextParts: string[]): Promise<void> => {
  await expect(control).toHaveAttribute('role', 'switch');
  await expect(control).toHaveAttribute('aria-checked', 'false');
  const describedBy = await control.getAttribute('aria-describedby');
  expect(describedBy, 'Consent switch must describe both purpose and lawful basis.').toBeTruthy();

  const descriptionText = await control.page().evaluate((ids) =>
    ids
      .split(/\s+/)
      .map((id) => document.getElementById(id)?.textContent?.trim() ?? '')
      .join(' '), describedBy ?? '');

  for (const expected of expectedTextParts) {
    expect(descriptionText).toContain(expected);
  }
};

interface AdminPortalE2eSnapshot {
  updateRequests: AdminPortalRequestCapture[];
  addConsentRequests: AdminPortalRequestCapture[];
  revokeConsentRequests: AdminPortalRequestCapture[];
  exportRequests: AdminPortalRequestCapture[];
  processingRecordRequests: AdminPortalRequestCapture[];
  erasureRequests: AdminPortalRequestCapture[];
  cancelErasureRequests: AdminPortalRequestCapture[];
}

interface AdminPortalRequestCapture {
  kind: 'update' | 'add-consent' | 'revoke-consent' | 'export' | 'processing-records' | 'erasure' | 'cancel-erasure';
  query: string | null;
  page: number;
  pageSize: number;
  type: string | null;
  active: boolean | null;
  partyId: string | null;
}

interface GdprRequestCounts {
  addConsent: number;
  revokeConsent: number;
  exportData: number;
  erasure: number;
  cancelErasure: number;
}

declare global {
  interface Window {
    HexalithPartiesConsumerPortal?: {
      downloadJson: (fileName: string, contentType: string, streamReference?: unknown) => Promise<void>;
    };
    __consumerPrivacyExportDownload?: {
      fileName: string;
      contentType: string;
    };
  }
}
