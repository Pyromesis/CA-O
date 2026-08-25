import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const source = fs.readFileSync(path.join(root, 'src/lib/optimization-commands.ts'), 'utf8');
const apiSource = fs.readFileSync(path.join(root, 'src/app/api/optimization/route.ts'), 'utf8');

const section = (text, start, end) => {
  const startIndex = text.indexOf(start);
  const endIndex = text.indexOf(end, startIndex + start.length);
  return startIndex >= 0 && endIndex > startIndex ? text.slice(startIndex, endIndex) : '';
};

const realCommandsSection = section(source, 'const baseRealCommands', 'export const realCommands');
const verificationSection = section(source, 'export const verificationCommands', 'export const revertCommands');
const revertSection = `${section(source, 'const baseRevertCommands', 'export const revertCommands')}\n${section(source, 'export const revertCommands', 'export function isExecutableOptimizationId')}`;
const revertVerificationSection = section(source, 'export const revertVerificationCommands', 'const baseRevertCommands');
const originalStateSection = section(source, 'export const originalStateCommands', 'export const antiCheatWarnings');
const nonExecutableSection = section(source, 'export const nonExecutableOptimizationIds', 'const baseVerificationCommands');
const irreversibleSection = section(source, 'export const irreversibleOptimizationIds', 'export const securityImpactById');
const guidanceSection = section(source, 'export const nonExecutableReasonById', 'const baseVerificationCommands');

if (!realCommandsSection || !verificationSection || !revertSection || !revertVerificationSection || !originalStateSection || !nonExecutableSection || !irreversibleSection || !guidanceSection) {
  throw new Error('Could not locate optimization command registries');
}

const ids = (text) => [...text.matchAll(/^\s*['"]([a-z][a-z0-9-]*)['"]\s*:/gm)].map((match) => match[1]);
const quotedIds = (text) => [...text.matchAll(/['"]([a-z0-9-]+)['"]/g)].map((match) => match[1]);
const realIds = new Set(ids(realCommandsSection));
const verificationIds = new Set(ids(verificationSection));
const revertIds = new Set(ids(revertSection));
const revertVerificationIds = new Set(ids(revertVerificationSection));
const originalStateIds = new Set(ids(originalStateSection));
const nonExecutableIds = new Set(quotedIds(nonExecutableSection));
const irreversibleIds = new Set(quotedIds(irreversibleSection));
const guidanceIds = new Set(ids(guidanceSection));

const categorySection = section(apiSource, 'const categoryOptimizations', '// Generate full optimization items from commands');
const categoryIds = new Set(quotedIds(categorySection));

const executableIds = [...realIds].filter((id) => !nonExecutableIds.has(id));
const duplicateRealIds = ids(realCommandsSection).filter((id, index, all) => all.indexOf(id) !== index);
const missingGuidanceReasons = [...nonExecutableIds].filter((id) => !guidanceIds.has(id));
const missingCategories = [...realIds].filter((id) => !categoryIds.has(id));

const missingVerification = [...realIds].filter((id) => !verificationIds.has(id));
const missingRevert = [...realIds].filter((id) => !revertIds.has(id) && !irreversibleIds.has(id));
const missingRevertVerification = [...realIds].filter((id) => !revertVerificationIds.has(id) && !nonExecutableIds.has(id) && !irreversibleIds.has(id));
const missingOriginalState = ['disable-startup-sound', 'disable-cortana', 'disable-screen-saver', 'disable-mouse-trails', 'hide-task-view', 'disable-aero-peek', 'disable-tooltips', 'disable-wallpaper-slideshow', 'disable-system-sounds', 'show-hidden-files', 'disable-start-menu-suggestions', 'disable-taskbar-search', 'show-file-extensions', 'disable-background-apps', 'disable-cast-notifications', 'disable-thumbnails', 'disable-lock-screen', 'disable-advertising-id', 'disable-tailored-experiences', 'disable-windows-feedback', 'disable-cloud-content', 'disable-start-tracking', 'disable-app-suggestions', 'disable-setting-sync', 'disable-handwriting-data', 'disable-speech-recognition', 'disable-find-my-device', 'disable-diagnostic-data', 'disable-camera-access', 'disable-microphone-access'].filter((id) => !originalStateIds.has(id));
const unsafeExposed = [...nonExecutableIds].filter((id) => !source.includes(`!nonExecutableOptimizationIds.has(id)`));

if (duplicateRealIds.length || missingVerification.length || missingRevert.length || missingRevertVerification.length || missingOriginalState.length || unsafeExposed.length || missingGuidanceReasons.length || missingCategories.length) {
  console.error(JSON.stringify({ duplicateRealIds, missingVerification, missingRevert, missingRevertVerification, missingOriginalState, unsafeExposed, missingGuidanceReasons, missingCategories }, null, 2));
  process.exit(1);
}

console.log(`Optimization contract OK: ${realIds.size} command entries, ${executableIds.length} executable entries, ${verificationIds.size} verification entries, ${revertIds.size} revert entries, ${revertVerificationIds.size} revert verifiers.`);
