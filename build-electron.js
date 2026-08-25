/**
 * CA-O Windows Optimizer - Build EXE Script
 * Creates a distributable folder with exe launcher
 */
const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const ROOT = __dirname;
const DIST = path.join(ROOT, 'dist', 'CA-O-Windows-Optimizer');
const STANDALONE = path.join(ROOT, '.next', 'standalone');
const NODE_RUNTIME = path.join(DIST, 'node.exe');

console.log('🔨 Building CA-O Windows Optimizer .exe package...\n');

// Step 1: Clean dist
if (fs.existsSync(path.join(ROOT, 'dist'))) {
  fs.rmSync(path.join(ROOT, 'dist'), { recursive: true, force: true });
}
fs.mkdirSync(DIST, { recursive: true });

console.log('📦 Step 0: Bundling Node.js runtime...');
const nodeCommand = process.platform === 'win32' ? 'where.exe' : 'which';
const nodePath = execSync(`"${nodeCommand}" node`, { encoding: 'utf8' }).split(/\r?\n/).map((value) => value.trim()).find(Boolean);
if (!nodePath || !fs.existsSync(nodePath)) {
  throw new Error('Node.js runtime was not found. Install Node.js before building the installer.');
}
fs.copyFileSync(nodePath, NODE_RUNTIME);

console.log('📦 Step 1: Copying standalone build...');
for (const stalePath of ['dist', 'dist-electron', 'src', 'tests', 'prisma', 'build-electron.js', 'electron-main.js', 'package-lock.json', 'tsconfig.json']) {
  const target = path.join(STANDALONE, stalePath);
  if (fs.existsSync(target)) fs.rmSync(target, { recursive: true, force: true });
}
fs.copyFileSync(path.join(STANDALONE, 'server.js'), path.join(DIST, 'server.js'));
copyDirSync(path.join(STANDALONE, '.next'), path.join(DIST, '.next'));
for (const fileName of ['package.json', '.env', 'optimization-state.json']) {
  const source = path.join(STANDALONE, fileName);
  if (fs.existsSync(source)) fs.copyFileSync(source, path.join(DIST, fileName));
}

const tracedDependenciesSrc = path.join(STANDALONE, 'node_modules');
const tracedDependenciesDst = path.join(DIST, 'server-dependencies');
console.log('📦 Step 1b: Copying server dependencies...');
copyDirSync(tracedDependenciesSrc, tracedDependenciesDst);

// Step 2: Copy static files
console.log('📦 Step 2: Copying static assets...');
const staticSrc = path.join(ROOT, '.next', 'static');
const staticDst = path.join(DIST, '.next', 'static');
if (fs.existsSync(staticSrc)) {
  copyDirSync(staticSrc, staticDst);
}

// Step 3: Copy public folder
console.log('📦 Step 3: Copying public assets...');
const publicSrc = path.join(ROOT, 'public');
const publicDst = path.join(DIST, 'public');
if (fs.existsSync(publicSrc)) {
  copyDirSync(publicSrc, publicDst);
}

// Step 4: Copy database
console.log('📦 Step 4: Copying database...');
const dbSrc = path.join(ROOT, 'db');
const dbDst = path.join(DIST, 'db');
if (fs.existsSync(dbSrc)) {
  copyDirSync(dbSrc, dbDst);
}

// Step 5: Copy prisma schema
console.log('📦 Step 5: Copying Prisma schema...');
const prismaSrc = path.join(ROOT, 'prisma');
const prismaDst = path.join(DIST, 'prisma');
if (fs.existsSync(prismaSrc)) {
  copyDirSync(prismaSrc, prismaDst);
}

// Step 6: Copy .env
const envSrc = path.join(ROOT, '.env');
if (fs.existsSync(envSrc)) {
  fs.copyFileSync(envSrc, path.join(DIST, '.env'));
}

console.log('📦 Standalone resources prepared for electron-builder.');

console.log('\n✅ Build complete!');
console.log(`📂 Output: ${DIST}`);
console.log('\n📝 electron-builder will create the elevated Windows installer.');

function copyDirSync(src, dest) {
  if (!fs.existsSync(src)) return;
  fs.cpSync(src, dest, { recursive: true, force: true, dereference: true });
}
