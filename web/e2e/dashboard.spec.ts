import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';
import { schoolToday } from './school-date';

async function post(page: Page, path: string, data: object) {
  const { token } = await (await page.request.get('/api/v1/antiforgery')).json();
  const response = await page.request.post(path, { data, headers: { 'X-XSRF-TOKEN': token } });
  expect(response.status(), path).toBeLessThan(300);
  return response.status() === 204 ? null : response.json();
}
async function login(page: Page) {
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
  if (!session.permissions.includes('finance.read')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((a: { id: string }) => a.id === session.userId);
    await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
      profile: 'administrator',
      version: me.version,
      reason: 'Prueba sintética del resumen operativo.',
    });
    await login(page);
  }
}

test('dashboard prioritizes real work and keeps the selected cycle across navigation and refresh', async ({
  page,
}, testInfo) => {
  test.setTimeout(60_000);
  await login(page);
  const suffix = randomUUID().slice(0, 6);
  const number = 2060 + (testInfo.project.name === 'mobile' ? 1 : 0);
  const year = (
    await post(page, '/api/v1/academic/years', {
      label: 'Resumen ' + number,
      startsOn: `${number}-01-10`,
      endsOn: `${number}-10-30`,
    })
  ).id;
  await post(page, `/api/v1/academic/years/${year}/periods`, {
    startsOn: `${number}-01-10`,
    endsOn: `${number}-03-10`,
  });
  const detail = await (await page.request.get(`/api/v1/academic/years/${year}`)).json();
  await post(page, `/api/v1/academic/years/${year}/status`, {
    version: detail.year.version,
    status: 'EnrollmentOpen',
    reason: 'Preparación sintética de matrícula.',
  });
  const grade = (
    await post(page, '/api/v1/academic/grades', {
      code: 'D' + suffix,
      name: 'Grado de resumen ' + suffix,
      sequence: 90,
      stage: '',
    })
  ).id;
  const subject = (
    await post(page, '/api/v1/academic/subjects', {
      code: 'D' + suffix,
      name: 'Curso del resumen ' + suffix,
    })
  ).id;
  await post(page, `/api/v1/years/${year}/courses`, {
    gradeLevelId: grade,
    sectionId: null,
    subjectIds: [subject],
  });
  const student = (
    await post(page, '/api/v1/people', {
      givenNames: 'Resumen',
      familyNames: 'Sintético ' + suffix,
      studentCode: 'DASH-' + suffix,
      contacts: [],
      isGuardian: false,
    })
  ).id;
  const enrollment = (
    await post(page, '/api/v1/enrollments', {
      studentId: student,
      academicYearId: year,
      gradeLevelId: grade,
      sectionId: null,
      enrolledOn: `${number}-01-10`,
    })
  ).id;
  const concept = (
    await post(page, '/api/v1/finance/concepts', {
      code: 'D' + suffix,
      name: 'Cargo de resumen ' + suffix,
      isRecurring: false,
      countsForClearance: false,
    })
  ).id;
  const charge = (
    await post(page, '/api/v1/finance/charges', {
      enrollmentId: enrollment,
      conceptId: concept,
      billingPeriodId: null,
      occurrence: 1,
      amount: 450,
      dueOn: `${number}-02-05`,
      description: 'Cargo sintético',
    })
  ).id;
  await post(page, '/api/v1/finance/payments', {
    idempotencyKey: randomUUID().replaceAll('-', ''),
    payerName: 'Pagador sintético',
    method: 'Cash',
    receivedOn: await schoolToday(page),
    amount: 150,
    allocations: [{ chargeId: charge, amount: 150 }],
  });
  await page.goto(`/workspace?year=${year}`);
  await expect(page.getByRole('combobox', { name: 'Ciclo escolar', exact: true })).toHaveValue(
    year,
  );
  await expect(page.locator('.dash-metrics')).toContainText('150.00');
  await expect(page.getByRole('heading', { name: /Requiere atención/ })).toBeVisible();
  await expect(page.locator('.dash-attention')).toContainText('1 curso sin docente asignado');
  await expect(page.getByRole('heading', { name: 'Tus módulos' })).toHaveCount(0);
  await expect(
    page.getByRole('region', { name: 'Composición del saldo', exact: true }),
  ).toContainText('300.00');
  await expect(page.locator('.dash-periods')).toContainText('Próximo');
  for (const width of [320, 768, 1024, 1920]) {
    await page.setViewportSize({ width, height: 1000 });
    if (width === 320) {
      await page.getByText('Ver importes por día', { exact: true }).click();
      await expect(page.locator('.dash-chart-data dl')).toContainText('150.00');
      expect(await page.locator('.dash-attention-list li strong').evaluate(el => el.getBoundingClientRect().width)).toBeGreaterThan(100);
    }
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
      `overflow at ${width}`,
    ).toBeTruthy();
    expect(
      (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
        .violations,
    ).toEqual([]);
    await page.screenshot({
      path: testInfo.outputPath('dashboard-' + width + '.png'),
      fullPage: true,
    });
  }
  await page.getByRole('link', { name: 'Revisar cursos', exact: true }).click();
  await expect(page).toHaveURL(new RegExp('/workspace/courses\\?year=' + year));
  await page.goBack();
  await page.getByRole('button', { name: 'Actualizar', exact: true }).click();
  await expect(page.locator('.dash-metrics')).toContainText('150.00');
  await expect(page.getByRole('combobox', { name: 'Ciclo escolar', exact: true })).toHaveValue(
    year,
  );
});

test('dashboard load failures remain actionable instead of showing zero figures', async ({
  page,
}) => {
  await login(page);
  await page.route('**/api/v1/dashboard*', (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Resumen temporalmente no disponible.' }),
    }),
  );
  await page.goto('/workspace');
  await expect(page.getByRole('alert')).toContainText('No pudimos cargar el resumen');
  await expect(page.locator('.dash-metrics')).toHaveCount(0);
  await page.unroute('**/api/v1/dashboard*');
  await page.getByRole('button', { name: 'Reintentar', exact: true }).click();
  await expect(page.locator('.dash-context')).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});
