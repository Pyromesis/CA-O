const { app, BrowserWindow, shell, dialog, net: electronNet } = require('electron');
const { spawn, execFileSync } = require('child_process');
const nodeNet = require('net');
const path = require('path');
const fs = require('fs');

// Prevent multiple instances
const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
  app.quit();
}

let mainWindow = null;
let splash = null;
let serverProcess = null;
let PORT = 3000;
const HOST = '127.0.0.1';
const launcherLog = path.join(app.getPath('temp'), 'ca-o-launcher.log');

function log(message) {
  const line = `[${new Date().toISOString()}] ${message}\n`;
  try { fs.appendFileSync(launcherLog, line, 'utf8'); } catch (error) { console.error(error); }
  console.log(message);
}

// Get the resources path
function getResourcesPath() {
  if (app.isPackaged) {
    return path.join(process.resourcesPath, 'standalone');
  }
  return path.join(__dirname, '.next', 'standalone');
}

// Find node executable
function findNodeExe() {
  if (app.isPackaged) {
    const bundledNode = path.join(process.resourcesPath, 'node.exe');
    if (fs.existsSync(bundledNode)) {
      console.log(`[launcher] Using bundled node: ${bundledNode}`);
      return bundledNode;
    }
  }
  // Try system node via 'where'
  try {
    const result = execFileSync('where', ['node'], { encoding: 'utf-8', windowsHide: true });
    const nodePath = result.trim().split('\n')[0].trim();
    if (fs.existsSync(nodePath)) {
      console.log(`[launcher] Found node: ${nodePath}`);
      return nodePath;
    }
  } catch (e) { /* not in PATH */ }

  // Common install locations
  const commonPaths = [
    'C:\\Program Files\\nodejs\\node.exe',
    'C:\\Program Files (x86)\\nodejs\\node.exe',
    path.join(process.env.LOCALAPPDATA || '', 'Programs\\nodejs\\node.exe'),
    path.join(process.env.LOCALAPPDATA || '', 'pi-node\\current\\node.exe'),
  ];
  for (const p of commonPaths) {
    if (p && fs.existsSync(p)) {
      console.log(`[launcher] Found node: ${p}`);
      return p;
    }
  }
  return null;
}

// Start the Next.js server
function startServer() {
  const standaloneDir = getResourcesPath();
  const serverJs = path.join(standaloneDir, 'server.js');

  if (!fs.existsSync(serverJs)) {
    dialog.showErrorBox('CA-O Windows Optimizer', `server.js not found:\n${serverJs}`);
    app.quit();
    return false;
  }

  const dataDir = app.getPath('userData');
  const statePath = path.join(dataDir, 'optimization-state.json');
  const bundledStatePath = path.join(standaloneDir, 'optimization-state.json');
  fs.mkdirSync(dataDir, { recursive: true });
  if (!fs.existsSync(statePath) && fs.existsSync(bundledStatePath)) {
    fs.copyFileSync(bundledStatePath, statePath);
  }
  const serverDependencies = path.join(process.resourcesPath, 'server-dependencies');
  const nodeExe = findNodeExe();

  if (!nodeExe) {
    dialog.showErrorBox('CA-O Windows Optimizer', 'Node.js not found.\nPlease install Node.js from https://nodejs.org');
    app.quit();
    return false;
  }

  // Build clean env without Electron vars that might confuse Next.js
  const cleanEnv = {};
  for (const [key, value] of Object.entries(process.env)) {
    if (!key.startsWith('ELECTRON') && key !== 'ORIGINAL_XDG_CURRENT_DESKTOP') {
      cleanEnv[key] = value;
    }
  }

  log(`[launcher] Starting: ${nodeExe} ${serverJs} on ${HOST}:${PORT}`);

  serverProcess = spawn(nodeExe, ['--max-old-space-size=256', serverJs], {
    cwd: standaloneDir,
    env: {
      ...cleanEnv,
      NODE_ENV: 'production',
      PORT: String(PORT),
      HOSTNAME: HOST,
      NODE_PATH: serverDependencies,
      CAO_STATE_PATH: statePath,
    },
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  });

  serverProcess.stdout.on('data', (data) => log(`[server] ${data.toString().trim()}`));
  serverProcess.stderr.on('data', (data) => log(`[server:error] ${data.toString().trim()}`));
  serverProcess.on('error', (err) => {
    log(`[launcher] Spawn error: ${err.stack || err.message}`);
    if (splash && !splash.isDestroyed()) splash.close();
    dialog.showErrorBox('CA-O Windows Optimizer', `No se pudo iniciar el servidor:\n${err.message}`);
    app.quit();
  });
  serverProcess.on('close', (code) => {
    log(`[launcher] Server exited: ${code}`);
    serverProcess = null;
    if (mainWindow && !mainWindow.isDestroyed()) {
      dialog.showErrorBox('CA-O Windows Optimizer', `El servidor se cerró inesperadamente${code === null ? '.' : ` (código ${code}).`}`);
      app.quit();
    }
  });

  return serverProcess.pid > 0;
}

// Wait for server using Electron's net module (works with Chromium's network stack)
function waitForServer(maxRetries = 60) {
  return new Promise((resolve) => {
    let retries = 0;

    function check() {
      if (!serverProcess) {
        resolve(false);
        return;
      }

      const request = electronNet.request(`http://${HOST}:${PORT}`);

      request.on('response', (response) => {
        log(`[launcher] Server ready! Status: ${response.statusCode}`);
        // Consume data to close properly
        response.on('data', () => {});
        response.on('end', () => {});
        resolve(true);
      });

      request.on('error', (err) => {
        retries++;
        if (!serverProcess) {
          log(`[launcher] Server stopped while waiting: ${err.message}`);
          resolve(false);
          return;
        }
        if (retries < maxRetries) {
          setTimeout(check, 500);
        } else {
          log(`[launcher] Server timeout after ${maxRetries} retries: ${err.message}`);
          resolve(false);
        }
      });

      request.end();
    }

    // Wait 2 seconds for the server process to initialize
    setTimeout(check, 2000);
  });
}

// A stable port keeps browser-storage (localStorage) bound to the same
// origin between launches, so onboarding flags and user settings survive.
const PREFERRED_PORT = 38957;

function listenOn(port) {
  return new Promise((resolve, reject) => {
    const probe = nodeNet.createServer();
    probe.once('error', reject);
    probe.listen(port, HOST, () => {
      const address = probe.address();
      const resolvedPort = typeof address === 'object' && address ? address.port : null;
      probe.close((error) => error ? reject(error) : resolve(resolvedPort));
    });
  });
}

async function findAvailablePort() {
  try {
    return await listenOn(PREFERRED_PORT);
  } catch {
    // Preferred port busy; fall back to any free port.
    return listenOn(0);
  }
}

function isAppUrl(url) {
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'http:' && parsed.hostname === HOST && parsed.port === String(PORT);
  } catch {
    return false;
  }
}

// Splash screen HTML
function getSplashHTML() {
  return `data:text/html;charset=utf-8,${encodeURIComponent(`<!DOCTYPE html>
<html><body style="margin:0;display:flex;align-items:center;justify-content:center;height:100vh;
background:linear-gradient(135deg,#0f0f23,#1a1a3e,#0d0d1a);
font-family:'Segoe UI',sans-serif;color:white;-webkit-app-region:drag;overflow:hidden">
<div style="text-align:center">
<div style="font-size:48px;margin-bottom:12px">⚡</div>
<h1 style="margin:0 0 8px;font-size:24px;font-weight:700">CA-O Windows Optimizer</h1>
<p style="margin:0 0 24px;opacity:.6;font-size:13px">v0.2.1</p>
<div style="width:200px;height:4px;background:rgba(255,255,255,.1);border-radius:4px;overflow:hidden;margin:0 auto">
<div style="width:40%;height:100%;background:linear-gradient(90deg,#6366f1,#a855f7,#6366f1);border-radius:4px;animation:l 1.5s ease-in-out infinite"></div>
</div>
<p style="margin-top:16px;opacity:.5;font-size:12px">Iniciando servidor...</p>
</div>
<style>@keyframes l{0%{transform:translateX(-100%)}100%{transform:translateX(350%)}}</style>
</body></html>`)}`;
}

// Create main window
function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1400,
    height: 900,
    minWidth: 1024,
    minHeight: 700,
    title: 'CA-O Windows Optimizer',
    autoHideMenuBar: true,
    backgroundColor: '#0a0a0a',
    show: false,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
    },
  });

  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
    mainWindow.focus();
    // Close splash
    if (splash && !splash.isDestroyed()) {
      splash.close();
      splash = null;
    }
  });

  // Retry on failure
  let loadRetries = 0;
  mainWindow.webContents.on('did-fail-load', (event, errorCode, errorDescription) => {
    console.error(`[launcher] Page load failed (${loadRetries}): ${errorDescription}`);
    loadRetries++;
    if (loadRetries < 10) {
      setTimeout(() => {
        if (mainWindow && !mainWindow.isDestroyed()) {
          mainWindow.loadURL(`http://${HOST}:${PORT}`);
        }
      }, 2000);
    }
  });

  mainWindow.webContents.on('will-navigate', (event, url) => {
    if (!isAppUrl(url)) {
      event.preventDefault();
      if (url.startsWith('https://') || url.startsWith('http://')) shell.openExternal(url);
    }
  });

  // External links in browser
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (!isAppUrl(url)) {
      if (url.startsWith('https://') || url.startsWith('http://')) shell.openExternal(url);
      return { action: 'deny' };
    }
    if (isAppUrl(url)) {
      return { action: 'allow' };
    }
    if (url.startsWith('http')) {
      shell.openExternal(url);
      return { action: 'deny' };
    }
    return { action: 'deny' };
  });

  mainWindow.on('closed', () => { mainWindow = null; });
}

// Main lifecycle
app.on('ready', async () => {
  console.log(`[launcher] Ready. Packaged: ${app.isPackaged}`);

  // Show splash
  splash = new BrowserWindow({
    width: 500, height: 350,
    frame: false, resizable: false,
    backgroundColor: '#0f0f23',
    webPreferences: { nodeIntegration: false, contextIsolation: true },
  });
  splash.loadURL(getSplashHTML());

  // Reserve a free local port before starting Next.js.
  try {
    PORT = await findAvailablePort();
  } catch (error) {
    if (splash && !splash.isDestroyed()) splash.close();
    dialog.showErrorBox('CA-O Windows Optimizer', `No se pudo reservar un puerto local:\n${error.message}`);
    app.quit();
    return;
  }

  // Start server
  if (!startServer()) return;

  // Wait for server using Electron's network stack
  const ready = await waitForServer();

  if (!ready) {
    if (splash && !splash.isDestroyed()) splash.close();
    dialog.showErrorBox('CA-O Windows Optimizer',
      `El servidor no respondió a tiempo en el puerto ${PORT}.`);
    if (serverProcess) serverProcess.kill();
    app.quit();
    return;
  }

  // Create main window and load app
  createWindow();
  mainWindow.loadURL(`http://${HOST}:${PORT}`);
});

// Handle second instance
app.on('second-instance', () => {
  if (mainWindow) {
    if (mainWindow.isMinimized()) mainWindow.restore();
    mainWindow.focus();
  }
});

// Cleanup
app.on('before-quit', () => {
  if (serverProcess) { serverProcess.kill('SIGTERM'); serverProcess = null; }
});

app.on('window-all-closed', () => {
  if (serverProcess) { serverProcess.kill('SIGTERM'); serverProcess = null; }
  app.quit();
});
