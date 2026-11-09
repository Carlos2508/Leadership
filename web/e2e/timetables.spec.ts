import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { courseFixture, post, reason } from './support/school';

async function accessible(page: Page) {
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
}

test('prepare a complete week, safely retry, publish, print and reuse its structure', async ({
  page,
}, info) => {
  test.setTimeout(100_000);
  const number = info.project.name === 'mobile' ? 2100 : 2099;
  const f = await courseFixture(page, number);
  const staff = (
    await post(page, '/api/v1/people', {
      givenNames: 'Elena',
      familyNames: 'Docente ' + f.suffix,
      contacts: [],
      studentCode: null,
      isGuardian: false,
      confirmDuplicate: true,
      staffPosition: 'Docente',
    })
  ).id;
  await post(page, `/api/v1/courses/${f.course}/teachers`, {
    staffId: staff,
    role: 'Lead',
    startsOn: `${number}-01-10`,
    reason,
  });
  await page.goto(`/workspace/timetables?year=${f.yearId}&grade=${f.grade}`);
  await expect(
    page.getByRole('heading', { name: 'Aún no hay un horario publicado' }),
  ).toBeVisible();
  await page.getByRole('link', { name: 'Preparar primer horario' }).click();
  await page.getByRole('button', { name: 'Preparar estructura', exact: true }).click();
  const dialog = page.getByRole('dialog');
  for (const [name, start, end] of [
    ['Primer período', '08:00', '08:45'],
    ['Recreo', '08:45', '09:00'],
    ['Segundo período', '09:00', '09:45'],
  ]) {
    await dialog.getByRole('textbox', { name: 'Nombre del período', exact: true }).fill(name);
    await dialog.getByLabel('Inicio', { exact: true }).fill(start);
    await dialog.getByLabel('Fin', { exact: true }).fill(end);
    await dialog.getByRole('button', { name: 'Agregar período' }).click();
  }
  await dialog.getByText('Guardar estructura para otros grupos', { exact: true }).click();
  await dialog.getByLabel('Nombre de la estructura', { exact: true }).fill('Matutina ' + f.suffix);
  await dialog.getByRole('button', { name: 'Guardar estructura', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Estructura guardada');
  await accessible(page);
  await page.screenshot({ path: info.outputPath('structure.png'), fullPage: true });
  await dialog.getByRole('button', { name: 'Aplicar estructura', exact: true }).click();
  await page.getByRole('button', { name: 'Asignar Lunes 08:00', exact: true }).click();
  await dialog.getByRole('combobox', { name: 'Materia', exact: true }).selectOption(f.course);
  await expect(dialog.getByRole('combobox', { name: 'Docente', exact: true })).not.toHaveValue('');
  for (const day of ['Martes', 'Miércoles', 'Jueves', 'Viernes'])
    await dialog.getByRole('checkbox', { name: day, exact: true }).check();
  await dialog.getByText('Registrar un aula', { exact: true }).click();
  await dialog.getByLabel('Nombre del aula', { exact: true }).fill('Aula ' + f.suffix);
  await dialog.getByRole('button', { name: 'Agregar aula', exact: true }).click();
  await expect(
    dialog.getByRole('combobox', { name: 'Aula (opcional)', exact: true }),
  ).not.toHaveValue('');
  await dialog.getByRole('button', { name: 'Aplicar bloque', exact: true }).click();
  await page.getByRole('button', { name: 'Asignar Lunes 08:45', exact: true }).click();
  await dialog.getByRole('button', { name: 'Recreo / descanso', exact: true }).click();
  for (const day of ['Martes', 'Miércoles', 'Jueves', 'Viernes'])
    await dialog.getByRole('checkbox', { name: day, exact: true }).check();
  await dialog.getByRole('button', { name: 'Aplicar bloque', exact: true }).click();
  await accessible(page);
  await page.screenshot({ path: info.outputPath('weekly-editor.png'), fullPage: true });
  await page.route(
    `**/api/v1/years/${f.yearId}/timetables`,
    async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue();
        return;
      }
      const saved = await route.fetch();
      expect(saved.status()).toBe(200);
      await route.abort('connectionfailed');
    },
    { times: 1 },
  );
  await page.getByRole('button', { name: 'Guardar borrador', exact: true }).click();
  await expect(
    page.getByRole('button', { name: 'Reintentar operación', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Reintentar operación', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Publicar', exact: true })).toBeEnabled();
  const summaries = await (await page.request.get(`/api/v1/years/${f.yearId}/timetables`)).json();
  expect(summaries.filter((s: { gradeId: string }) => s.gradeId === f.grade)).toHaveLength(1);
  await page.getByRole('button', { name: 'Publicar', exact: true }).click();
  await page.getByRole('textbox', { name: 'Motivo de publicación', exact: true }).fill(reason);
  await page.getByRole('button', { name: 'Publicar horario', exact: true }).click();
  await expect(page).toHaveURL(/\/workspace\/timetables\?/);
  await expect(page.locator('app-timetable-board')).toBeVisible();
  await accessible(page);
  await page.screenshot({ path: info.outputPath('published.png'), fullPage: true });
  await page.getByRole('button', { name: 'Imprimir horario', exact: true }).click();
  await accessible(page);
  const href = await dialog.getByRole('link', { name: 'Abrir PDF' }).getAttribute('href');
  const pdf = await page.request.get(href!);
  expect(pdf.status()).toBe(200);
  expect((await pdf.body()).subarray(0, 4).toString()).toBe('%PDF');
  await page.keyboard.press('Escape');
  await page.getByRole('link', { name: 'Preparar horario', exact: true }).click();
  // On phones the board shows one day at a time: the other days' blocks exist but are hidden.
  await expect(
    page
      .locator('.time-block:visible')
      .filter({ hasText: 'Curso ' + f.suffix })
      .first(),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Copiar de otro horario', exact: true }).click();
  await expect(dialog.getByRole('combobox', { name: /Horario de origen/ })).toBeVisible();
  await dialog
    .getByRole('combobox', { name: 'Horario de origen' })
    .selectOption(
      (await (await page.request.get(`/api/v1/years/${f.yearId}/timetables`)).json()).find(
        (s: { gradeId: string }) => s.gradeId === f.grade,
      ).publishedId,
    );
  await dialog.getByRole('combobox', { name: 'Qué copiar' }).selectOption('all');
  await dialog.getByRole('button', { name: 'Preparar copia', exact: true }).click();
  await page.getByRole('button', { name: 'Copiar horario', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Publicar', exact: true })).toBeDisabled();
  await expect(
    page
      .locator('.time-block:visible')
      .filter({ hasText: 'Curso ' + f.suffix })
      .first(),
  ).toBeVisible();
});

test('a failed schedule date hides old content and can be retried', async ({ page }, info) => {
  const f = await courseFixture(page, info.project.name === 'mobile' ? 2100 : 2099);
  await page.goto(`/workspace/timetables?year=${f.yearId}&grade=${f.grade}`);
  await expect(
    page.getByRole('heading', { name: 'Aún no hay un horario publicado' }),
  ).toBeVisible();
  await page.route(
    '**/timetables/published?**',
    (route) =>
      route.fulfill({
        status: 503,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Conexión interrumpida para prueba' }),
      }),
    { times: 1 },
  );
  await page
    .getByRole('textbox', { name: 'Vigente al', exact: true })
    .fill('' + (info.project.name === 'mobile' ? 2100 : 2099) + '-02-01');
  await expect(page.getByRole('alert')).toContainText('Conexión interrumpida');
  await expect(page.locator('app-timetable-board')).toHaveCount(0);
  await page.getByRole('button', { name: 'Reintentar', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Aún no hay un horario publicado' }),
  ).toBeVisible();
});
