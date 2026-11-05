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

// Precompress content-hashed text bundles once at build time (Brotli max quality + gzip) so the host sends them
// without compressing per request (see SpaAssets). index.html is not precompressed: it is tiny and must revalidate.
import { brotliCompressSync, gzipSync, constants } from 'node:zlib';
import { readFileSync, writeFileSync } from 'node:fs';
const hashed = /^[A-Za-z0-9_]+-[A-Za-z0-9_-]{8,}\.(js|css|svg)$/;
let saved = 0;
for (const name of readdirSync(target)) {
  if (!hashed.test(name)) continue;
  const content = readFileSync(resolve(target, name));
  if (content.length < 1024) continue;
  const br = brotliCompressSync(content, { params: { [constants.BROTLI_PARAM_QUALITY]: 11, [constants.BROTLI_PARAM_SIZE_HINT]: content.length } });
  writeFileSync(resolve(target, name + '.br'), br);
  writeFileSync(resolve(target, name + '.gz'), gzipSync(content, { level: 9 }));
  saved += content.length - br.length;
}
console.log(`Precompressed bundles (${Math.round(saved / 1024)} KB less with Brotli).`);
