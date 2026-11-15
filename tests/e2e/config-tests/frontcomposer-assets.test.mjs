import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { qualifyFrontComposerAssets } from '../helpers/frontcomposer-assets.ts';

const assets = ['js/fc-focus.js', 'js/fc-keyboard.js', 'js/fc-download.js', 'css/fc-shell.css'];

async function fixture(version, mismatch, check) {
  const root = await mkdtemp(path.join(tmpdir(), 'parties-asset-guard-'));
  try {
    await writeFile(path.join(root, 'Hexalith.Parties.UI.source-metadata.txt'), 'false\npackage\n\n');
    const packages = path.join(root, 'packages');
    await writeFile(path.join(root, 'Hexalith.Parties.UI.package-metadata.txt'), `${version}\n${packages}\n`);
    for (const asset of assets) {
      const selected = path.join(packages, 'hexalith.frontcomposer.shell', version, 'staticwebassets', asset);
      await mkdir(path.dirname(selected), { recursive: true });
      await writeFile(selected, `selected ${version} ${asset}`);
    }
    let requests = 0;
    const page = { request: { get: async url => {
      requests++;
      const asset = url.replace('/_content/Hexalith.FrontComposer.Shell/', '');
      return { ok: () => true, body: async () => Buffer.from(asset === mismatch ? 'foreign served bytes' : `selected ${version} ${asset}`) };
    } } };
    const attachments = [];
    const info = { attach: async (name, value) => attachments.push({ name, ...value }) };
    await check(() => qualifyFrontComposerAssets(page, info, pathToFileURL(root + path.sep)), () => requests, attachments);
  } finally { await rm(root, { recursive: true, force: true }); }
}

for (const version of ['5.0.0', '6.0.0']) {
  test(`exact ${version} compares all served assets and retains hashes`, async () => {
    await fixture(version, null, async (qualify, requests, attachments) => {
      assert.equal(await qualify(), 'package');
      assert.equal(requests(), 4);
      const receipt = JSON.parse(attachments[0].body);
      assert.equal(receipt.version, version);
      assert.equal(receipt.assets.length, 4);
      for (const [index, asset] of assets.entries()) {
        assert.equal(receipt.assets[index].sha256, createHash('sha256').update(`selected ${version} ${asset}`).digest('hex'));
      }
    });
  });
}

for (const version of ['4.9.9', '5.0.1', '6.0.1', '6.0.0-beta.1', '6.0.0+foreign', '7.0.0']) {
  test(`unqualified ${version} refuses before served-asset access`, async () => {
    await fixture(version, null, async (qualify, requests, attachments) => {
      await assert.rejects(qualify, /Only the exact retained 5.0.0 and current 6.0.0/);
      assert.equal(requests(), 0);
      assert.equal(attachments.length, 0);
    });
  });
}

for (const asset of assets) {
  test(`6.0.0 refuses mismatched served ${asset}`, async () => {
    await fixture('6.0.0', asset, async (qualify, _requests, attachments) => {
      await assert.rejects(qualify, /Served package bytes/);
      assert.equal(attachments.length, 0);
    });
  });
}
