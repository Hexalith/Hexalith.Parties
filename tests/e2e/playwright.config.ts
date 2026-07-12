import { defineConfig, devices } from '@playwright/test';

const BASE_URL = process.env.BASE_URL ?? 'http://127.0.0.1:5072';
const IS_CI = !!process.env.CI;

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  forbidOnly: IS_CI,
  retries: IS_CI ? 2 : 0,
  workers: 1,
  timeout: 60_000,
  expect: {
    timeout: 10_000,
  },
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['junit', { outputFile: 'test-results/junit.xml' }],
  ],
  use: {
    baseURL: BASE_URL,
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ignoreHTTPSErrors: true,
    testIdAttribute: 'data-testid',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  outputDir: 'test-results',
  webServer: process.env.PLAYWRIGHT_SKIP_WEBSERVER
    ? undefined
    : {
        command: 'dotnet run --project ../../src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj --configuration Release --no-build --no-launch-profile --urls http://127.0.0.1:5072',
        url: BASE_URL,
        reuseExistingServer: !IS_CI,
        timeout: 120_000,
        env: {
          ASPNETCORE_ENVIRONMENT: 'Test',
          Hexalith__Parties__AccessibilitySpecimens__Enabled: 'true',
          Hexalith__Parties__AdminPortalE2E__Enabled: 'true',
          Parties__BaseUrl: 'http://127.0.0.1:59999',
          Parties__Tenant: 'test-tenant',
        },
      },
});
