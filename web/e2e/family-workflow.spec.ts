import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';
import { courseFixture, login, post, year } from './support/school';

async function accessible(page: Page) {
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
}
async function setup(page: Page, number: number, capacity = 10) {
  await login(page);
  const suffix = randomUUID().slice(0, 7);
  const yearId = await year(page, number);
  const grade = (
    await post(page, '/api/v1/academic/grades', {
      code: 'F' + suffix,
      name: 'Grado familia ' + suffix,
      stage: '',
      sequence: 88,
    })
  ).id;
  const section = (
    await post(page, `/api/v1/academic/years/${yearId}/sections`, {
      gradeLevelId: grade,
      name: 'A',
      capacity,
    })
  ).id;
  await page.goto(`/workspace/new-family?year=${yearId}`);
  await expect(page.getByRole('heading', { name: 'Encargado 1', exact: true })).toBeVisible();
  return { yearId, grade, section, suffix };
}
async function guardian(page: Page, suffix: string) {
  const box = page.locator('[data-person="guardian-0"]');
  await box.getByLabel('Nombres', { exact: true }).fill('Madre ' + suffix);
  await box.getByLabel('Apellidos', { exact: true }).fill('Familia sintética');
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
}
async function student(
  page: Page,
  index: number,
  given: string,
  suffix: string,
  grade: string,
  section: string,
) {
  const box = page.locator(`[data-person="student-${index}"]`);
  await box.getByLabel('Nombres', { exact: true }).fill(given);
  await box.getByLabel('Apellidos', { exact: true }).fill('Familia ' + suffix);
  await box.getByLabel('Código del estudiante').fill(given + suffix);
  await box.getByLabel('Grado', { exact: true }).selectOption(grade);
  await box.getByLabel('Sección', { exact: true }).selectOption(section);
}

test('one family registers siblings, survives a lost reply, reuses the guardian and prints cards without filtering', async ({
  page,
}, info) => {
  test.setTimeout(90_000);
  const f = await setup(page, 2096);
  await accessible(page);
  await page.screenshot({ path: info.outputPath('family-guardians.png'), fullPage: true });
  await guardian(page, f.suffix);
  await student(page, 0, 'Ana', f.suffix, f.grade, f.section);
  await page.getByRole('button', { name: 'Agregar estudiante', exact: true }).click();
  await student(page, 1, 'Luis', f.suffix, f.grade, f.section);
  await accessible(page);
  await page.screenshot({ path: info.outputPath('family-students.png'), fullPage: true });
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await accessible(page);
  await page.route(
    '**/api/v1/people/families',
    async (route) => {
      const saved = await route.fetch();
      expect(saved.status()).toBe(200);
      await route.abort('connectionfailed');
    },
    { times: 1 },
  );
  await page.getByRole('button', { name: 'Confirmar familia e inscripciones' }).click();
  await expect(page.getByRole('button', { name: 'Reintentar registro' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Anterior', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Reintentar registro' }).click();
  await expect(
    page.getByRole('heading', { name: 'Familia registrada', exact: true }),
  ).toBeVisible();
  const roster = await (
    await page.request.get(`/api/v1/years/${f.yearId}/enrollments?gradeId=${f.grade}`)
  ).json();
  expect(roster.items).toHaveLength(2);
  const first = await (
    await page.request.get('/api/v1/people/' + roster.items[0].studentId)
  ).json();
  expect(first.guardians).toHaveLength(1);
  expect(first.guardians[0].faculties).toEqual([]);
  await page.getByRole('link', { name: 'Ver inscripciones' }).click();
  await expect(page.getByRole('combobox', { name: 'Grado', exact: true })).toHaveValue('');
  await page.getByRole('button', { name: 'Imprimir carnets', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('combobox', { name: 'Grado', exact: true }).selectOption(f.grade);
  await dialog.getByRole('combobox', { name: 'Sección', exact: true }).selectOption(f.section);
  await accessible(page);
  await page.screenshot({ path: info.outputPath('cards-shortcut.png'), fullPage: true });
  const href = await dialog.getByRole('link', { name: 'Abrir PDF' }).getAttribute('href');
  const pdf = await page.request.get(href!);
  expect(pdf.status()).toBe(200);
  expect((await pdf.body()).subarray(0, 4).toString()).toBe('%PDF');
  await page.keyboard.press('Escape');
  await page.goto(`/workspace/new-family?year=${f.yearId}`);
  const parent = page.locator('[data-person="guardian-0"]');
  await parent.getByText('Buscar una ficha existente', { exact: true }).click();
  await parent.getByLabel('Buscar por nombre o código').fill(f.suffix);
  await parent.getByRole('button', { name: 'Buscar ficha', exact: true }).click();
  await parent.locator('.family-candidate').click();
  await expect(parent).toContainText('Ficha existente');
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await student(page, 0, 'Mara', f.suffix, f.grade, f.section);
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar familia e inscripciones' }).click();
  await expect(
    page.getByRole('heading', { name: 'Familia registrada', exact: true }),
  ).toBeVisible();
  const parents = await (
    await page.request.get(`/api/v1/people?role=guardian&search=${f.suffix}`)
  ).json();
  expect(parents.items).toHaveLength(1);
});

test('a full section leaves no half family and preserves the form for correction', async ({
  page,
}, info) => {
  test.setTimeout(60_000);
  const f = await setup(page, 2097, 1);
  await guardian(page, f.suffix);
  await student(page, 0, 'Ana', f.suffix, f.grade, f.section);
  await page.getByRole('button', { name: 'Agregar estudiante', exact: true }).click();
  await student(page, 1, 'Luis', f.suffix, f.grade, f.section);
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar familia e inscripciones' }).click();
  await expect(page.getByRole('alert')).toContainText(/cupo/i);
  const parents = await (
    await page.request.get(`/api/v1/people?role=guardian&search=${f.suffix}`)
  ).json();
  expect(parents.items).toHaveLength(0);
  await page.getByRole('button', { name: 'Anterior', exact: true }).click();
  await expect(
    page.locator('[data-person="student-1"]').getByLabel('Nombres', { exact: true }),
  ).toHaveValue('Luis');
  await page
    .locator('[data-person="student-1"]')
    .getByLabel('Sección', { exact: true })
    .selectOption('');
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar familia e inscripciones' }).click();
  await expect(
    page.getByRole('heading', { name: 'Familia registrada', exact: true }),
  ).toBeVisible();
});

test('coordination opens the course with missing grades and error can be retried', async ({
  page,
}, info) => {
  test.setTimeout(60_000);
  const f = await courseFixture(page, 2098);
  await page.goto(`/workspace/grades?year=${f.yearId}`);
  await page.route(
    '**/course-completion',
    (route) =>
      route.fulfill({
        status: 503,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Prueba de conexión interrumpida' }),
      }),
    { times: 1 },
  );
  await page.getByRole('button', { name: /Ver pendientes por curso/ }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('alert')).toBeVisible();
  await dialog.getByRole('button', { name: 'Reintentar', exact: true }).click();
  await expect(dialog).toContainText('2 pendientes');
  await accessible(page);
  await page.screenshot({ path: info.outputPath('grade-pending.png'), fullPage: true });
  await dialog.locator('a[href*="' + f.course + '"]').click();
  await expect(page).toHaveURL(new RegExp(`/courses/${f.course}/grades`));
  await expect(page.locator('.score-input')).toHaveCount(2);
});
