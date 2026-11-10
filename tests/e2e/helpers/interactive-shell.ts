import { expect, type Page } from '@playwright/test';

/** Waits for the FrontComposer shell's first interactive render so clicks reach Blazor handlers instead of prerendered markup. */
export async function expectInteractiveShell(page: Page): Promise<void> {
  await expect(page.locator('.fc-shell-root[data-fc-interactive="true"]')).toBeVisible();
}
