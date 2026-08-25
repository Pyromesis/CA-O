/**
 * Generates a minimal CycloneDX-style SBOM from package-lock.json (v2).
 * Dependency transparency without external tooling: lists direct and
 * transitive dependencies with resolved versions.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const lockPath = path.join(root, 'package-lock.json');

if (!fs.existsSync(lockPath)) {
  console.error('package-lock.json not found; run npm install first.');
  process.exit(1);
}

const lock = JSON.parse(fs.readFileSync(lockPath, 'utf8'));
const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'));

const components = Object.entries(lock.packages ?? {})
  .filter(([key]) => key.startsWith('node_modules/'))
  .map(([key, info]) => ({
    type: 'library',
    name: key.replace(/^node_modules\//, '').replace(/node_modules\//g, '/'),
    version: info.version,
    purl: `pkg:npm/${key.replace(/^node_modules\//, '')}@${info.version}`,
  }));

const sbom = {
  bomFormat: 'CycloneDX',
  specVersion: '1.5',
  metadata: {
    timestamp: new Date().toISOString(),
    component: { type: 'application', name: pkg.name, version: pkg.version },
    tools: [{ name: 'scripts/generate-sbom.mjs', version: '1.0' }],
  },
  components,
};

const outPath = path.join(root, 'sbom.json');
fs.writeFileSync(outPath, JSON.stringify(sbom, null, 2), 'utf8');
console.log(`SBOM written to sbom.json (${components.length} components).`);
