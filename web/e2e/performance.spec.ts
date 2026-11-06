import { expect, test, type Page } from '@playwright/test';

async function login(page: Page, username: string, password: string): Promise<void> {
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Usuario', { exact: true }).fill(username);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(page.getByRole('heading', { name: /^¡Hola,/ })).toBeVisible();
}

// Budget for a local SQL Server and an unthrottled browser; a regression past it is worth investigating.
const apiBudgetMs = 750;
const pageBudgetMs = 1500;

test('bundles are precompressed and cached, pages and API stay within budget', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name === 'mobile', 'Measured once, on desktop.');
  const owner = process.env['SCHOOLPLATFORM_E2E_USERNAME']!;
  const ownerPassword = process.env['SCHOOLPLATFORM_E2E_PASSWORD']!;
  await login(page, owner, ownerPassword);
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('people.read')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((x: { id: string }) => x.id === session.userId);
    const csrf = (await (await page.request.get('/api/v1/antiforgery')).json()).token as string;
    const changed = await page.request.post(`/api/v1/access/accounts/${me.id}/profile`, {
      data: { version: me.version, reason: 'Autorización institucional sintética para prueba de navegador.', profile: 'administrator' },
      headers: { 'X-XSRF-TOKEN': csrf },
    });
    expect(changed.status()).toBeLessThan(300);
    await login(page, owner, ownerPassword);
  }

  // Shell revalidates; hashed bundles are immutable and sent precompressed.
  const shell = await page.request.get('/workspace');
  expect(shell.headers()['cache-control']).toBe('no-cache');
  const html = await shell.text();
  const main = /src="(main-[A-Za-z0-9_-]+\.js)"/.exec(html)?.[1];
  expect(main).toBeTruthy();
  for (const [encoding, expected] of [
    ['br', 'br'],
    ['gzip', 'gzip'],
  ]) {
    const bundle = await page.request.get('/' + main, { headers: { 'Accept-Encoding': encoding } });
    expect(bundle.status()).toBe(200);
    expect(bundle.headers()['content-encoding']).toBe(expected);
    expect(bundle.headers()['cache-control']).toContain('immutable');
    expect(bundle.headers()['content-type']).toContain('javascript');
    expect((await bundle.body()).length).toBeGreaterThan(1000);
  }
  const plain = await page.request.get('/' + main, { headers: { 'Accept-Encoding': 'identity' } });
  expect(plain.headers()['content-encoding']).toBeUndefined();

  // API JSON is compressed; the antiforgery endpoint never is.
  const people = await page.request.get('/api/v1/people?page=1', { headers: { 'Accept-Encoding': 'br' } });
  expect(people.status()).toBe(200);
  expect(people.headers()['content-encoding'], JSON.stringify(people.headers())).toBe('br');
  const token = await page.request.get('/api/v1/antiforgery', { headers: { 'Accept-Encoding': 'br' } });
  expect(token.headers()['content-encoding']).toBeUndefined();

  const timings: { url: string; ms: number }[] = [];
  page.on('requestfinished', (request) => {
    if (!request.url().includes('/api/')) return;
    const timing = request.timing();
    timings.push({ url: new URL(request.url()).pathname, ms: Math.round(timing.responseEnd - timing.requestStart) });
  });
  const pages: { path: string; ms: number }[] = [];
  for (const path of ['/workspace', '/workspace/people', '/workspace/academic', '/workspace/enrollment', '/workspace/courses',
    '/workspace/grades', '/workspace/finance', '/workspace/finance/setup', '/workspace/finance/clearance', '/workspace/announcements',
    '/workspace/access', '/workspace/audit']) {
    const started = Date.now();
    await page.goto(path);
    await expect(page.locator('h1').first()).toBeVisible();
    await expect(page.locator('.loading-state')).toHaveCount(0);
    pages.push({ path, ms: Date.now() - started });
  }
  await testInfo.attach('timings.json', { body: JSON.stringify({ pages, api: timings }, null, 2), contentType: 'application/json' });
  console.log(pages.map((x) => `${x.path}: ${x.ms} ms`).join('\n'));
  const slowest = [...timings].sort((a, b) => b.ms - a.ms).slice(0, 5);
  console.log('Slowest API:', slowest.map((x) => `${x.url} ${x.ms} ms`).join(', '));
  for (const x of timings) expect(x.ms, x.url).toBeLessThan(apiBudgetMs);
  for (const x of pages) expect(x.ms, x.path).toBeLessThan(pageBudgetMs);
});
