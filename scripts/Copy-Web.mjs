import { cpSync, mkdirSync, readdirSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
const source = fileURLToPath(new URL('../web/dist/school-platform/browser/', import.meta.url));
const target = fileURLToPath(new URL('../src/SchoolPlatform.Host/wwwroot/app/', import.meta.url));
const root = fileURLToPath(new URL('../', import.meta.url));
if (!target.startsWith(resolve(root) + '\\') && !target.startsWith(resolve(root) + '/')) throw new Error('Destino fuera del repositorio.');
mkdirSync(target, { recursive: true });
// Delete only the generated app directory, after validating its resolved absolute path.
for (const entry of readdirSync(target)) rmSync(resolve(target, entry), { recursive: true, force: true });
cpSync(source, target, { recursive: true });
