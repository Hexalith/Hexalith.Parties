import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { once } from 'node:events';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const run = promisify(execFile);
const workspace = fileURLToPath(new URL('../', import.meta.url));
const configUrl = new URL('../playwright.config.ts', import.meta.url).href;

function environment(overrides) {
  const env = { ...process.env };
  for (const key of ['CI', 'ASPNETCORE_ENVIRONMENT', 'PLAYWRIGHT_SKIP_WEBSERVER', 'BASE_URL']) {
    delete env[key];
  }
  return { ...env, ...overrides };
}

async function readConfig(overrides) {
  const { stdout } = await run(process.execPath, [
    '--experimental-strip-types', '--input-type=module', '--eval',
    `const { default: config } = await import(${JSON.stringify(configUrl)}); console.log(JSON.stringify(config));`,
  ], { cwd: workspace, env: environment(overrides), timeout: 15_000 });
  return JSON.parse(stdout);
}

for (const ci of [false, true]) {
  for (const parentEnvironment of ['Test', 'Development']) {
    test(`managed server: CI=${ci}, parent=${parentEnvironment}`, async () => {
      const config = await readConfig({ CI: ci ? 'true' : '', ASPNETCORE_ENVIRONMENT: parentEnvironment });
      const server = config.webServer;
      assert.equal(server.reuseExistingServer, false);
      assert.equal(server.url, config.use.baseURL);
      assert.equal(server.env.ASPNETCORE_ENVIRONMENT, 'Test');
      assert.equal(server.env.Hexalith__Parties__AccessibilitySpecimens__Enabled, 'true');
      assert.equal(server.env.Hexalith__Parties__AdminPortalE2E__Enabled, 'true');
      assert.match(server.command, /--no-launch-profile/);
      if (ci) {
        assert.match(server.command, /--configuration Release --no-build --no-restore/);
        assert.doesNotMatch(server.command, /UseHexalithProjectReferences=true/);
      } else {
        assert.match(server.command, /--configuration Debug/);
        assert.match(server.command, /UseHexalithProjectReferences=true/);
      }
    });
  }
  test(`explicit external server: CI=${ci}`, async () => {
    const config = await readConfig({
      CI: ci ? 'true' : '', PLAYWRIGHT_SKIP_WEBSERVER: '1', BASE_URL: 'http://127.0.0.1:5099',
    });
    assert.equal(config.webServer, undefined);
    assert.equal(config.use.baseURL, 'http://127.0.0.1:5099');
  });
}

for (const occupied of [true, false]) {
  test(occupied ? 'Playwright rejects an unrelated listener' : 'Playwright starts and cleans up its managed fixture', async () => {
    const listener = createServer((_request, response) => response.end('unrelated server'));
    const directory = await mkdtemp(path.join(tmpdir(), 'parties-server-ownership-'));
    try {
      listener.listen(0, '127.0.0.1');
      await once(listener, 'listening');
      const port = listener.address().port;
      const url = `http://127.0.0.1:${port}`;
      if (!occupied) {
        await new Promise(resolve => listener.close(resolve));
      }
      const fixtureConfig = path.join(directory, 'playwright.config.mjs');
      const playwrightApi = new URL('../node_modules/@playwright/test/index.mjs', import.meta.url).href;
      await writeFile(path.join(directory, 'ownership.spec.mjs'),
        `import { test, expect } from ${JSON.stringify(playwrightApi)};
         test('managed fixture is reachable', async ({ request }) => {
           const response = await request.get(${JSON.stringify(url)});
           expect(await response.text()).toBe('managed fixture');
         });`);
      await writeFile(path.join(directory, 'server.mjs'),
        `import { createServer } from 'node:http';
         createServer((_request, response) => response.end('managed fixture')).listen(${port}, '127.0.0.1');`);
      await writeFile(fixtureConfig, `
        import config from ${JSON.stringify(configUrl)};
        export default {
          ...config, testDir: ${JSON.stringify(directory)}, reporter: 'line', retries: 0,
          outputDir: ${JSON.stringify(path.join(directory, 'results'))},
          webServer: { ...config.webServer, url: ${JSON.stringify(url)}, cwd: ${JSON.stringify(directory)},
            command: ${JSON.stringify(occupied ? 'node -e "process.exit(97)"' : 'node server.mjs')}, timeout: 5000 },
        };
      `);
      const result = await run(process.execPath, [
        path.join(workspace, 'node_modules/@playwright/test/cli.js'), 'test', '--config', fixtureConfig,
      ], {
        cwd: workspace,
        env: environment({ ASPNETCORE_ENVIRONMENT: 'Test' }),
        timeout: 20_000,
      }).then(value => ({ ...value, code: 0 }), error => error);
      assert.equal(result.code, occupied ? 1 : 0, result.stdout + result.stderr);
      if (occupied) {
        assert.match(result.stdout + result.stderr, /already used/);
      } else {
        assert.match(result.stdout + result.stderr, /1 passed/);
        // Binding the same port proves that Playwright released the managed listener.
        listener.listen(port, '127.0.0.1');
        await once(listener, 'listening');
      }
    } finally {
      listener.closeAllConnections();
      await new Promise(resolve => listener.close(resolve));
      await rm(directory, { recursive: true, force: true });
    }
  });
}
