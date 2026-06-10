import { expect, test, type APIRequestContext, type BrowserContext, type Locator, type Page } from '@playwright/test';

const ADMIN_ROUTE = '/admin/parties';
const ADMIN_COOKIE = 'parties-admin-e2e';
const PICKER_ROUTE = '/__parties/specimens/party-picker';
const REQUESTS_ROUTE = '/__parties/specimens/admin-portal/requests';
const RESET_ROUTE = '/__parties/specimens/admin-portal/reset';
const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:5072';

test.describe('Party picker combobox', () => {
  test.use({
    locale: 'en-US',
    timezoneId: 'UTC',
  });

  test.beforeEach(async ({ request }) => {
    await request.post(RESET_ROUTE);
  });

  test('keyboard selection keeps focus on the input and emits a bounded event', async ({ page, request }) => {
    await gotoPicker(page);
    await capturePartySelectedEvents(page);

    const input = page.getByRole('combobox', { name: 'Search parties' });
    await input.fill('Ada');

    const listboxId = await expectPopupRelationship(input, page);
    await expect(page.locator(`#${listboxId}`)).toHaveAttribute('role', 'listbox');
    const option = page.getByRole('option', { name: /Ada Lovelace/ });
    await expect(option).toHaveAttribute('tabindex', '-1');

    await input.press('ArrowDown');
    await expect(input).toBeFocused();
    const activeId = await activeDescendantId(input);
    await expect(page.locator(`#${activeId}`)).toHaveAttribute('aria-selected', 'true');

    await input.press('Enter');

    await expect(input).toBeFocused();
    await expect(page.getByRole('group', { name: 'Selected party' })).toContainText('Ada Lovelace');
    await expect(page.getByRole('listbox', { name: 'Party search results' })).toHaveCount(0);

    const events = await selectedEvents(page);
    expect(events).toEqual([
      {
        bubbles: true,
        composed: true,
        detail: {
          partyId: 'ada-lovelace',
          partyType: 'Person',
          status: 'active',
        },
        keys: ['partyId', 'partyType', 'status'],
      },
    ]);

    await expect.poll(async () => (await latestPickerSearchRequest(request))?.query).toBe('Ada');
  });

  test('escape closes degraded results without changing durable selection', async ({ page, request }) => {
    await gotoPicker(page);

    const input = page.getByRole('combobox', { name: 'Search parties' });
    await input.fill('degraded');

    await expectPopupRelationship(input, page);
    await expect(page.getByRole('status').filter({ hasText: 'Limited search results' })).toBeVisible();
    await expect(page.getByRole('option', { name: /Degraded Partners/ })).toBeVisible();

    await input.press('ArrowDown');
    const activeId = await activeDescendantId(input);
    await expect(page.locator(`#${activeId}`)).toHaveAttribute('aria-selected', 'true');

    await input.press('Escape');

    await expect(input).toHaveAttribute('aria-expanded', 'false');
    await expect(input).not.toHaveAttribute('aria-activedescendant', /.+/);
    await expect(page.getByRole('listbox', { name: 'Party search results' })).toHaveCount(0);
    await expect(page.getByRole('group', { name: 'Selected party' })).toHaveCount(0);
    await expect.poll(async () => (await latestPickerSearchRequest(request))?.query).toBe('degraded');
  });

  test('admin form bridge receives the bounded party-selected event', async ({ page, context, request }) => {
    await request.post(RESET_ROUTE);
    await enableAdminFixture(context);

    await page.goto(`${ADMIN_ROUTE}/new`);
    await expect(page.getByRole('heading', { name: 'Create party' })).toBeVisible();

    const picker = page.locator('hexalith-party-picker');
    await expect(picker).toBeVisible();
    await picker.dispatchEvent('party-selected', {
      detail: {
        partyId: 'ada-lovelace',
        partyType: 'Person',
        status: 'active',
        displayName: 'Ada Lovelace',
        tenantId: 'tenant-a',
      },
      bubbles: true,
      composed: true,
    });

    await expect(page.getByRole('status').filter({ hasText: 'Person active' })).toBeVisible();
    await expect(page.getByText('Ada Lovelace')).toHaveCount(0);
    await expect(page.getByText('tenant-a')).toHaveCount(0);
  });
});

const gotoPicker = async (page: Page): Promise<void> => {
  await page.goto(PICKER_ROUTE);
  await expect(page.getByTestId('parties-picker-specimen-ready'), `${PICKER_ROUTE} missing ready marker`).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Search parties' })).toBeVisible();
};

const expectPopupRelationship = async (
  input: Locator,
  page: Page,
): Promise<string> => {
  await expect(input).toHaveAttribute('aria-expanded', 'true');
  const listboxId = await input.getAttribute('aria-controls');
  if (!listboxId) {
    throw new Error('Expected picker input to reference a listbox id.');
  }

  await expect(page.locator(`#${listboxId}`)).toBeVisible();
  return listboxId;
};

const activeDescendantId = async (input: Locator): Promise<string> => {
  const activeId = await input.getAttribute('aria-activedescendant');
  if (!activeId) {
    throw new Error('Expected picker input to expose aria-activedescendant.');
  }

  return activeId;
};

const capturePartySelectedEvents = async (page: Page): Promise<void> => {
  await page.evaluate(() => {
    window.__partyPickerEvents = [];
    document.addEventListener('party-selected', (event) => {
      const customEvent = event as CustomEvent<PartySelectedEventCapture['detail']>;
      window.__partyPickerEvents.push({
        bubbles: event.bubbles,
        composed: event.composed,
        detail: customEvent.detail,
        keys: Object.keys(customEvent.detail ?? {}).sort(),
      });
    });
  });
};

const selectedEvents = async (page: Page): Promise<PartySelectedEventCapture[]> =>
  await page.evaluate(() => window.__partyPickerEvents);

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

const latestPickerSearchRequest = async (request: APIRequestContext): Promise<AdminPortalRequestCapture | undefined> => {
  const snapshot = await requestSnapshot(request);
  return snapshot.pickerSearchRequests.at(-1);
};

const requestSnapshot = async (request: APIRequestContext): Promise<AdminPortalE2eSnapshot> => {
  const response = await request.get(REQUESTS_ROUTE);
  expect(response.ok()).toBe(true);
  return await response.json() as AdminPortalE2eSnapshot;
};

interface PartySelectedEventCapture {
  bubbles: boolean;
  composed: boolean;
  detail: {
    partyId: string;
    partyType: string;
    status: string;
  };
  keys: string[];
}

interface AdminPortalE2eSnapshot {
  pickerSearchRequests: AdminPortalRequestCapture[];
}

interface AdminPortalRequestCapture {
  query: string | null;
}

declare global {
  interface Window {
    __partyPickerEvents: PartySelectedEventCapture[];
  }
}
