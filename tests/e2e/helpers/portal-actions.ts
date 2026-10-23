import type { Locator, Page } from '@playwright/test';

/** Finds an operation independently from a same-named accordion header. */
export function portalAction(scope: Page | Locator, name: string): Locator {
  return scope.getByRole('button', { name, exact: true }).and(scope.locator('fluent-button'));
}
