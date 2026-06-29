import { expect, test, type BrowserContext, type Locator } from '@playwright/test';

const ADMIN_ROUTE = '/admin/parties';
const ADMIN_COOKIE = 'parties-admin-e2e';
const CONSUMER_COOKIE = 'parties-consumer-e2e';
const RESET_ROUTE = '/__parties/specimens/admin-portal/reset';
const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:5072';

test.describe('Shared portal display formatters', () => {
  test.use({
    locale: 'en-US',
    timezoneId: 'UTC',
  });

  test.beforeEach(async ({ page, request }) => {
    await request.post(RESET_ROUTE);
    await page.setViewportSize({ width: 1280, height: 900 });
  });

  test('Admin portal preserves compact dates and localized boolean labels', async ({ context, page }) => {
    await enableAdminFixture(context);

    await page.goto(ADMIN_ROUTE);

    const grid = page.getByRole('grid', { name: 'Parties' });
    await expect(grid.getByRole('button', { name: 'Ada Lovelace' })).toBeVisible();
    await expect(grid.getByText('6/10/2026 10:00 AM')).toBeVisible();
    await expect(grid.getByText('6/10/2026 11:00 AM')).toBeVisible();

    await grid.getByRole('button', { name: 'Grace Hopper' }).click();
    await page.waitForURL('**/admin/parties/grace-hopper');

    const detail = page.getByLabel('Party detail');
    await expectDefinition(sectionWithHeading(detail, 'Summary'), 'Created', '6/9/2026 10:00 AM');
    await expectDefinition(sectionWithHeading(detail, 'Summary'), 'Modified', '6/9/2026 11:00 AM');
    await expectDefinition(sectionWithHeading(detail, 'Restrictions'), 'Restricted', 'No');
    await expectDefinition(sectionWithHeading(detail, 'Restrictions'), 'Erased', 'No');
    await expectDefinition(sectionWithHeading(detail, 'Operational summary'), 'Restricted party', 'No');
    await expectDefinition(sectionWithHeading(detail, 'Operational summary'), 'Pending erasure', 'No');

    await detail.getByLabel('Restriction reason').fill('display formatter e2e');
    await detail.getByRole('button', { name: 'Restrict processing' }).click();
    await detail.getByRole('group', { name: 'Confirm restriction' }).getByRole('button', { name: 'Confirm' }).click();

    await expect(detail.getByRole('status').filter({ hasText: 'Saved - updating...' })).toBeVisible();
    await expectDefinition(sectionWithHeading(detail, 'Restrictions'), 'Restricted', 'Yes');
    await expectDefinition(sectionWithHeading(detail, 'Operational summary'), 'Restricted party', 'Yes');
  });

  test('Consumer portal preserves plain dates without Admin time density', async ({ context, page }) => {
    await enableConsumerFixture(context);

    await page.goto('/me');

    const recordDates = sectionWithHeading(page.locator('body'), 'Record dates');
    await expectDefinition(recordDates, 'Created', '6/10/2026');
    await expectDefinition(recordDates, 'Last updated', '6/10/2026');
    await expect(recordDates.getByText('6/10/2026 10:00 AM')).toHaveCount(0);
    await expect(recordDates.getByText('6/10/2026 11:00 AM')).toHaveCount(0);

    await page.goto('/me/edit');

    await expect(page.getByRole('heading', { name: 'Edit profile' })).toBeVisible();
    await expect(page.getByLabel('Date of birth')).toHaveValue('');
    await expect(page.getByLabel('Date of birth')).not.toHaveValue(/AM|PM/);
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

const sectionWithHeading = (scope: Locator, heading: string): Locator =>
  scope
    .locator(`xpath=.//section[*[self::h2 or self::h3 or self::h4 or self::h5][normalize-space(.)=${xpathLiteral(heading)}]]`)
    .first();

const expectDefinition = async (scope: Locator, term: string, value: string): Promise<void> => {
  const definitionTerm = scope.locator('dt').filter({ hasText: exactText(term) });
  await expect(definitionTerm.locator('xpath=following-sibling::dd[1]')).toHaveText(value);
};

const exactText = (value: string): RegExp => new RegExp(`^${escapeRegExp(value)}$`);

const escapeRegExp = (value: string): string => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

const xpathLiteral = (value: string): string => {
  if (!value.includes("'")) {
    return `'${value}'`;
  }

  if (!value.includes('"')) {
    return `"${value}"`;
  }

  return `concat(${value.split("'").map(part => `'${part}'`).join(', "\"\'\"", ')})`;
};
