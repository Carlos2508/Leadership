import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

async function post(page: Page, path: string, data: object) {
  const { token } = await (await page.request.get('/api/v1/antiforgery')).json();
  const response = await page.request.post(path, { data, headers: { 'X-XSRF-TOKEN': token } });
  expect(response.status(), path).toBeLessThan(300);
  return response.status() === 204 ? null : response.json();
}

async function login(page: Page): Promise<void> {
  await page.goto('/Identity/Account/Login');
  await page
    .getByLabel('Usuario', { exact: true })
    .fill(process.env['SCHOOLPLATFORM_E2E_USERNAME']!);
  await page
    .getByLabel('Contraseña', { exact: true })
    .fill(process.env['SCHOOLPLATFORM_E2E_PASSWORD']!);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(
    page.getByRole('heading', { name: /^(Buenos días|Buenas tardes|Buenas noches)/ }),
  ).toBeVisible();
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('finance.read')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((account: { id: string }) => account.id === session.userId);
    await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
      profile: 'administrator',
      version: me.version,
      reason: 'Prueba sintética de recuperación de Caja.',
    });
    await login(page);
  }
}

async function students(page: Page) {
  const suffix = randomUUID().slice(0, 8);
  return Promise.all(
    ['Primera', 'Segunda'].map(async (name, index) => {
      const code = `QA-${index}-${suffix}`;
      const person = await post(page, '/api/v1/people', {
        givenNames: name,
        familyNames: 'Cuenta ' + suffix,
        studentCode: code,
        contacts: [],
        isGuardian: false,
      });
      return { id: person.id as string, name: `${name} Cuenta ${suffix}`, code };
    }),
  );
}

async function choose(page: Page, student: { name: string; code: string }) {
  await page.getByRole('searchbox').fill(student.code);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.getByRole('button', { name: new RegExp(student.name) }).click();
}

test.beforeEach(async ({ page }) => login(page));

test('cashier follows student selection through browser back and forward', async ({ page }) => {
  const [first, second] = await students(page);
  await page.goto('/workspace/finance');
  await page.getByRole('searchbox').fill('SIN-COINCIDENCIAS-' + randomUUID());
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'No encontramos estudiantes con esa búsqueda.' })).toBeVisible();
  await choose(page, first);
  await expect(page.locator('#statement-title')).toContainText(first.name);
  await choose(page, second);
  await expect(page.locator('#statement-title')).toContainText(second.name);
  await page.goBack();
  await expect(page).toHaveURL(new RegExp('student=' + first.id));
  await expect(page.locator('#statement-title')).toContainText(first.name);
  await page.goBack();
  await expect(page).toHaveURL(/\/workspace\/finance$/);
  await expect(page.locator('.statement-card')).toHaveCount(0);
  await page.goForward();
  await expect(page.locator('#statement-title')).toContainText(first.name);
});

test('failed student lookup clears the previous statement and can be retried', async ({
  page,
}, testInfo) => {
  const [first, second] = await students(page);
  await page.goto('/workspace/finance');
  await choose(page, first);
  await expect(page.locator('#statement-title')).toContainText(first.name);
  const path = `**/api/v1/finance/students/${second.id}/statement`;
  await page.route(path, (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Estado de cuenta temporalmente no disponible.' }),
    }),
  );
  await choose(page, second);
  await expect(page.getByRole('alert')).toContainText(
    'Estado de cuenta temporalmente no disponible.',
  );
  await expect(page.locator('.statement-card')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Registrar pago', exact: true })).toHaveCount(0);
  await expect(page).toHaveURL(new RegExp('student=' + second.id));
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({
    path: testInfo.outputPath('cashier-statement-error.png'),
    fullPage: true,
  });
  await page.unroute(path);
  await page.getByRole('button', { name: 'Reintentar estado de cuenta', exact: true }).click();
  await expect(page.locator('#statement-title')).toContainText(second.name);
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('a late response cannot replace the currently selected student', async ({ page }) => {
  const [first, second] = await students(page);
  let release!: () => void;
  let requested!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const arrived = new Promise<void>((resolve) => {
    requested = resolve;
  });
  await page.route(`**/api/v1/finance/students/${first.id}/statement`, async (route) => {
    const response = await route.fetch();
    requested();
    await held;
    await route.fulfill({ response });
  });
  try {
    await page.goto('/workspace/finance');
    await choose(page, first);
    await arrived;
    await choose(page, second);
    await expect(page.locator('#statement-title')).toContainText(second.name);
    const response = page.waitForResponse((response) =>
      response.url().includes(`/students/${first.id}/statement`),
    );
    release();
    await response;
    // Wait until the response has reached Angular, including its next render.
    await page.waitForTimeout(150);
    await expect(page.locator('#statement-title')).toContainText(second.name);
    await expect(page).toHaveURL(new RegExp('student=' + second.id));
  } finally {
    release();
  }
});

test('receipt summary failures are visible and recoverable', async ({ page }, testInfo) => {
  const receipts = await (await page.request.get('/api/v1/finance/payments')).json();
  await page.route('**/api/v1/finance/payments', (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Cobros temporalmente no disponibles.' }),
    }),
  );
  await page.goto('/workspace/finance');
  const summary = page.getByRole('region', { name: 'Recibos de hoy' });
  await expect(summary.getByRole('alert')).toContainText('Cobros temporalmente no disponibles.');
  await expect(page.getByText('Aún no hay pagos registrados hoy.', { exact: true })).toHaveCount(0);
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy();
  await page.screenshot({
    path: testInfo.outputPath('cashier-receipts-error.png'),
    fullPage: true,
  });
  await page.unroute('**/api/v1/finance/payments');
  await summary.getByRole('button', { name: 'Reintentar recibos', exact: true }).click();
  await expect(summary.getByRole('alert')).toHaveCount(0);
  if (receipts.length) {
    await expect(summary.getByRole('link', { name: /^Ver recibo/ })).toHaveCount(receipts.length);
  } else {
    await expect(
      summary.getByText('Aún no hay pagos registrados hoy.', { exact: true }),
    ).toBeVisible();
  }
});
