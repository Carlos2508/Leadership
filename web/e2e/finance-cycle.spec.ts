import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

const reason = 'Comprobación sintética de contexto financiero por ciclo.';
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
    page.getByRole('heading', { name: /^¡Hola,/ }),
  ).toBeVisible();
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('finance.charges')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((account: { id: string }) => account.id === session.userId);
    await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
      profile: 'administrator',
      version: me.version,
      reason,
    });
    await login(page);
  }
}
async function prepare(page: Page, firstYear: number) {
  await login(page);
  const suffix = randomUUID().slice(0, 7);
  const result = [];
  for (const [index, name] of ['Alfa', 'Beta'].entries()) {
    const number = firstYear + index;
    const year = (
      await post(page, '/api/v1/academic/years', {
        label: `${name} ${suffix}`,
        startsOn: `${number}-01-10`,
        endsOn: `${number}-10-30`,
      })
    ).id;
    const period = (
      await post(page, `/api/v1/finance/years/${year}/periods`, {
        label: 'Cuota ' + name,
        dueOn: `${number}-01-20`,
      })
    ).id;
    const cutoff = (
      await post(page, `/api/v1/finance/years/${year}/cutoffs`, {
        name: 'Corte ' + name,
        throughSequence: 1,
      })
    ).id;
    result.push({ year, number, period, cutoff, name });
  }
  return result;
}
async function expectContext(
  page: Page,
  kind: string,
  context: { year: string; name: string; cutoff: string },
) {
  await expect(page.getByRole('combobox', { name: 'Ciclo', exact: true })).toHaveValue(
    context.year,
  );
  if (kind === 'setup') {
    await expect(page.locator('.period-list')).toContainText('Cuota ' + context.name);
  } else {
    await expect(page.getByRole('combobox', { name: 'Corte', exact: true })).toHaveValue(
      context.cutoff,
    );
    await expect(page.locator('.preview-summary')).toBeVisible();
  }
}

for (const [index, kind] of ['setup', 'clearance'].entries()) {
  test(`${kind} keeps an invalid cycle or cross-cycle cutoff visible as an error`, async ({
    page,
  }, testInfo) => {
    const [first, second] = await prepare(
      page,
      2016 + index * 4 + (testInfo.project.name === 'mobile' ? 2 : 0),
    );
    await page.goto(`/workspace/finance/${kind}?year=${randomUUID()}`);
    await expect(page.getByRole('alert')).toContainText('No encontramos el ciclo solicitado.');
    await expect(page.locator('.period-list, .preview-summary')).toHaveCount(0);
    await page.getByRole('combobox', { name: 'Ciclo', exact: true }).selectOption(first.year);
    await expectContext(page, kind, first);
    if (kind === 'clearance') {
      await page.goto(`/workspace/finance/clearance?year=${first.year}&cutoff=${second.cutoff}`);
      await expect(page.getByRole('alert')).toContainText(
        'El corte solicitado no pertenece a este ciclo.',
      );
      await expect(page.locator('.preview-summary')).toHaveCount(0);
      await page.getByRole('combobox', { name: 'Corte', exact: true }).selectOption(first.cutoff);
      await expectContext(page, kind, first);
      await page.getByRole('combobox', { name: 'Estado', exact: true }).selectOption('Blocked');
      await expect(page).toHaveURL(/status=Blocked/);
      await expectContext(page, kind, first);
      await page.goBack();
      await expect(page.getByRole('combobox', { name: 'Estado', exact: true })).toHaveValue('all');
      await expectContext(page, kind, first);
    }
  });
  test(`${kind} follows the selected cycle through browser history and refresh`, async ({
    page,
  }, testInfo) => {
    const [first, second] = await prepare(
      page,
      2000 + index * 4 + (testInfo.project.name === 'mobile' ? 2 : 0),
    );
    await page.goto(`/workspace/finance/${kind}?year=${first.year}`);
    await expectContext(page, kind, first);
    await page.getByRole('combobox', { name: 'Ciclo', exact: true }).selectOption(second.year);
    await expectContext(page, kind, second);
    await page.goBack();
    await expect(page).toHaveURL(new RegExp('year=' + first.year));
    await expectContext(page, kind, first);
    await page.reload();
    await expectContext(page, kind, first);
    if (kind === 'setup') {
      await page.getByRole('button', { name: '+ Agregar', exact: true }).click();
      await page
        .getByRole('dialog')
        .getByLabel('Nombre de la cuota')
        .fill('Verificación del ciclo');
      await page.getByRole('dialog').getByLabel('Vencimiento').fill(`${first.number}-02-20`);
      const request = page.waitForRequest(
        (request) =>
          request.method() === 'POST' && request.url().endsWith(`/years/${first.year}/periods`),
      );
      await page.getByRole('dialog').getByRole('button', { name: 'Guardar', exact: true }).click();
      await request;
      await expect(page.locator('.period-list')).toContainText('Verificación del ciclo');
      const other = await (
        await page.request.get(`/api/v1/finance/years/${second.year}/setup`)
      ).json();
      expect(other.periods).toHaveLength(1);
    }
  });

  test(`${kind} clears a failed cycle and ignores a previous slow response`, async ({
    page,
  }, testInfo) => {
    const [first, second] = await prepare(
      page,
      2008 + index * 4 + (testInfo.project.name === 'mobile' ? 2 : 0),
    );
    await page.goto(`/workspace/finance/${kind}?year=${first.year}`);
    await expectContext(page, kind, first);
    const endpoint = `**/api/v1/finance/years/${second.year}/setup`;
    await page.route(endpoint, (route) =>
      route.fulfill({
        status: 503,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Consulta del ciclo temporalmente no disponible.' }),
      }),
    );
    await page.getByRole('combobox', { name: 'Ciclo', exact: true }).selectOption(second.year);
    await expect(page.getByRole('alert')).toContainText(
      'Consulta del ciclo temporalmente no disponible.',
    );
    await expect(
      page.getByText('Este ciclo no tiene cortes de solvencia', { exact: true }),
    ).toHaveCount(0);
    await expect(page.locator('.period-list, .preview-summary')).toHaveCount(0);
    await page.unroute(endpoint);
    await page
      .getByRole('button', {
        name: kind === 'setup' ? 'Reintentar configuración' : 'Reintentar solvencia',
        exact: true,
      })
      .click();
    await expectContext(page, kind, second);
    let release!: () => void;
    let requested!: () => void;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    const arrived = new Promise<void>((resolve) => {
      requested = resolve;
    });
    await page.route(`**/api/v1/finance/years/${first.year}/setup`, async (route) => {
      const response = await route.fetch();
      requested();
      await held;
      await route.fulfill({ response });
    });
    try {
      await page.getByRole('combobox', { name: 'Ciclo', exact: true }).selectOption(first.year);
      await arrived;
      await page.getByRole('combobox', { name: 'Ciclo', exact: true }).selectOption(second.year);
      await expectContext(page, kind, second);
      const delivered = page.waitForResponse((response) =>
        response.url().endsWith(`/years/${first.year}/setup`),
      );
      release();
      await delivered;
      await page.waitForTimeout(150);
      await expectContext(page, kind, second);
      expect(
        (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
          .violations,
      ).toEqual([]);
      await page.screenshot({
        path: testInfo.outputPath(kind + '-cycle-recovered.png'),
        fullPage: true,
      });
    } finally {
      release();
    }
  });
}
