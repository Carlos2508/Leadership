// Generates docs/manual/Logica_de_negocio.pdf from docs/manual/logica-de-negocio.html with Chromium (Playwright).
// Run from the repo: .tools/node/node_modules/node/bin/node.exe scripts/Build-Manual.mjs
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
const require = createRequire(new URL('../web/package.json', import.meta.url));
const { chromium } = require('@playwright/test');
const source = fileURLToPath(new URL('../docs/manual/logica-de-negocio.html', import.meta.url));
const target = fileURLToPath(new URL('../docs/manual/Logica_de_negocio.pdf', import.meta.url));
const browser = await chromium.launch();
const page = await browser.newPage();
await page.goto(pathToFileURL(source).href, { waitUntil: 'networkidle' });
await page.evaluate(() => document.fonts.ready);
await page.pdf({
  path: target,
  format: 'Letter',
  printBackground: true,
  preferCSSPageSize: true,
  displayHeaderFooter: true,
  headerTemplate: '<span></span>',
  footerTemplate:
    '<div style="width:100%;font-size:7px;color:#586e59;padding:0 17mm;display:flex;justify-content:space-between;font-family:Segoe UI,sans-serif"><span>Plataforma escolar · Lógica de negocio</span><span><span class="pageNumber"></span> / <span class="totalPages"></span></span></div>',
});
await browser.close();
console.log('PDF:', target);
