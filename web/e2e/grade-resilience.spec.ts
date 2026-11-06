import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

const reason = 'Prueba sintética de conservación de notas.';
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
  if (!session.permissions.includes('academic.manage')) {
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

async function prepare(page: Page, yearNumber: number) {
  await login(page);
  const suffix = randomUUID().slice(0, 7);
  const year = (
    await post(page, '/api/v1/academic/years', {
      label: 'Conservación ' + suffix,
      startsOn: `${yearNumber}-01-10`,
      endsOn: `${yearNumber}-10-30`,
    })
  ).id;
  await post(page, `/api/v1/academic/years/${year}/periods`, {
    startsOn: `${yearNumber}-01-10`,
    endsOn: `${yearNumber}-03-10`,
  });
  await post(page, `/api/v1/academic/years/${year}/periods`, {
    startsOn: `${yearNumber}-03-11`,
    endsOn: `${yearNumber}-05-10`,
  });
  const detail = await (await page.request.get(`/api/v1/academic/years/${year}`)).json();
  await post(page, `/api/v1/academic/years/${year}/status`, {
    version: detail.year.version,
    status: 'EnrollmentOpen',
    reason,
  });
  const grade = (
    await post(page, '/api/v1/academic/grades', {
      code: 'R' + suffix,
      name: 'Grado ' + suffix,
      stage: '',
      sequence: 80,
    })
  ).id;
  const subject = (
    await post(page, '/api/v1/academic/subjects', { code: 'R' + suffix, name: 'Libreta ' + suffix })
  ).id;
  await post(page, `/api/v1/years/${year}/courses`, {
    gradeLevelId: grade,
    sectionId: null,
    subjectIds: [subject],
  });
  const course = (
    await (await page.request.get(`/api/v1/years/${year}/courses?gradeId=${grade}`)).json()
  )[0].id;
  const student = (
    await post(page, '/api/v1/people', {
      givenNames: 'Nota',
      familyNames: 'Sintética ' + suffix,
      contacts: [],
      studentCode: 'REG-' + suffix,
      isGuardian: false,
    })
  ).id;
  await post(page, '/api/v1/enrollments', {
    studentId: student,
    academicYearId: year,
    gradeLevelId: grade,
    sectionId: null,
    enrolledOn: `${yearNumber}-01-10`,
  });
  const path = `/workspace/courses/${course}/grades`;
  await page.goto(path + '?period=' + detail.periods[0].id);
  return { path, course, first: detail.periods[0].id, second: detail.periods[1].id };
}

test('a failed autosave cannot discard grades when changing period or leaving the sheet', async ({
  page,
}, testInfo) => {
  const { path, course, first, second } = await prepare(
    page,
    testInfo.project.name === 'mobile' ? 2087 : 2086,
  );
  const input = page.getByRole('textbox', { name: /^Nota de Nota Sintética/ });
  await expect(input).toBeEnabled();
  const endpoint = `**/api/v1/courses/${course}/grades`;
  await page.route(endpoint, (route) =>
    route.request().method() === 'POST'
      ? route.fulfill({
          status: 503,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'No se pudo guardar la nota.' }),
        })
      : route.continue(),
  );
  await input.fill('83.5');
  await expect(page.getByRole('alert')).toContainText('No se pudo guardar la nota.');
  await page.getByRole('link', { name: 'Bimestre 2', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('Hay notas sin guardar');
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('grade-unsaved-warning.png'), fullPage: true });
  await page.getByRole('button', { name: 'Seguir editando', exact: true }).click();
  await expect(page).toHaveURL(new RegExp('period=' + first));
  await expect(input).toHaveValue('83.5');
  await page.getByRole('link', { name: 'Volver a la lista', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('Hay notas sin guardar');
  await page.getByRole('button', { name: 'Seguir editando', exact: true }).click();
  await expect(input).toHaveValue('83.5');
  await page.unroute(endpoint);
  await page.getByRole('button', { name: 'Guardar notas', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Guardar notas', exact: true })).toBeDisabled();
  await page.getByRole('link', { name: 'Bimestre 2', exact: true }).click();
  await expect(page).toHaveURL(new RegExp('period=' + second));
  await expect(input).toHaveValue('');
  await page.goBack();
  await expect(page).toHaveURL(new RegExp('period=' + first));
  await expect(input).toHaveValue('83.5');
  await page.reload();
  await expect(input).toHaveValue('83.5');
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('grade-recovered.png'), fullPage: true });
  const saved = await (
    await page.request.get(`/api/v1/courses/${course}/grades?periodId=${first}`)
  ).json();
  expect(saved.rows[0].score).toBe(83.5);
  expect(new URL(page.url()).pathname).toBe(path);
});

test('period changes wait for in-flight edits and discarding invalid notes requires confirmation', async ({
  page,
}, testInfo) => {
  const { course, first, second } = await prepare(
    page,
    testInfo.project.name === 'mobile' ? 2089 : 2088,
  );
  const input = page.getByRole('textbox', { name: /^Nota de Nota Sintética/ });
  let release!: () => void;
  let requested!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const arrived = new Promise<void>((resolve) => {
    requested = resolve;
  });
  let holdNext = true;
  await page.route(`**/api/v1/courses/${course}/grades`, async (route) => {
    if (route.request().method() !== 'POST' || !holdNext) return route.continue();
    holdNext = false;
    const response = await route.fetch();
    requested();
    await held;
    await route.fulfill({ response });
  });
  try {
    await input.fill('66');
    await arrived;
    // This newer edit must also reach SQL before replacing the sheet.
    await input.fill('67');
    await page.getByRole('link', { name: 'Bimestre 2', exact: true }).click();
    await expect(page).toHaveURL(new RegExp('period=' + first));
    release();
    await expect(page).toHaveURL(new RegExp('period=' + second));
    await expect(input).toHaveValue('');
    const saved = await (
      await page.request.get(`/api/v1/courses/${course}/grades?periodId=${first}`)
    ).json();
    expect(saved.rows[0].score).toBe(67);
    await input.fill('1000');
    await page.getByRole('link', { name: 'Bimestre 1', exact: true }).click();
    await expect(page.getByRole('dialog')).toContainText('Hay notas sin guardar');
    await page.getByRole('button', { name: 'Salir sin guardar', exact: true }).click();
    await expect(page).toHaveURL(new RegExp('period=' + first));
    await expect(input).toHaveValue('67');
    const empty = await (
      await page.request.get(`/api/v1/courses/${course}/grades?periodId=${second}`)
    ).json();
    expect(empty.rows[0].score).toBeNull();
    // A load failure has a visible retry and cannot show the previous period's scores.
    const failed = `**/api/v1/courses/${course}/grades?periodId=${second}`;
    await page.route(failed, (route) =>
      route.fulfill({
        status: 503,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Libreta temporalmente no disponible.' }),
      }),
    );
    await page.getByRole('link', { name: 'Bimestre 2', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText('Libreta temporalmente no disponible.');
    await expect(input).toHaveCount(0);
    await page.unroute(failed);
    await page.getByRole('button', { name: 'Reintentar libreta', exact: true }).click();
    await expect(input).toHaveValue('');
  } finally {
    release();
  }
});
