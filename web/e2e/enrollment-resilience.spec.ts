import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';
import { login, post, year } from './support/school';

async function prepare(page: Page, number: number) {
  await login(page);
  const suffix = randomUUID().slice(0, 6);
  const grade = (
    await post(page, '/api/v1/academic/grades', {
      code: 'E' + suffix,
      name: 'Grado inscripción ' + suffix,
      stage: '',
      sequence: 88,
    })
  ).id;
  const rows = [];
  for (const [index, givenNames] of ['Alfa', 'Beta'].entries()) {
    const id = await year(page, number + index);
    const name = givenNames + ' Matrícula ' + suffix;
    const student = (
      await post(page, '/api/v1/people', {
        givenNames,
        familyNames: 'Matrícula ' + suffix,
        studentCode: `E${index}${suffix}`,
        contacts: [],
        isGuardian: false,
      })
    ).id;
    await post(page, '/api/v1/enrollments', {
      studentId: student,
      academicYearId: id,
      gradeLevelId: grade,
      sectionId: null,
      enrolledOn: `${number + index}-01-10`,
    });
    rows.push({ id, name });
  }
  return rows;
}
async function expectYear(page: Page, context: { id: string; name: string }) {
  await expect(page.getByRole('combobox', { name: 'Ciclo', exact: true })).toHaveValue(context.id);
  await expect(page.locator('tbody')).toContainText(context.name);
}
test('enrollment cycle, search and status survive history; clearing filters keeps the current cycle', async ({
  page,
}, testInfo) => {
  const [first, second] = await prepare(page, testInfo.project.name === 'mobile' ? 2032 : 2028);
  await page.goto('/workspace/enrollment?year=' + first.id);
  await expectYear(page, first);
  await page.getByRole('combobox', { name: 'Ciclo', exact: true }).selectOption(second.id);
  await expectYear(page, second);
  await page.goBack();
  await expect(page).toHaveURL(new RegExp('year=' + first.id));
  await expectYear(page, first);
  await expect(page.locator('tbody')).not.toContainText(second.name);
  await page.getByRole('searchbox').fill(first.name);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await expect(page).toHaveURL(/search=/);
  await page.getByRole('combobox', { name: 'Estado', exact: true }).selectOption('all');
  await expect(page).toHaveURL(/status=all/);
  await page.goBack();
  await expect(page.getByRole('combobox', { name: 'Estado', exact: true })).toHaveValue('Active');
  await expect(page.getByRole('searchbox')).toHaveValue(first.name);
  await page.getByRole('button', { name: 'Limpiar filtros', exact: true }).click();
  await expect(page.getByRole('searchbox')).toHaveValue('');
  await expectYear(page, first);
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('enrollment-context.png'), fullPage: true });
});
test('failed enrollment cycles hide old records and actions; retries and slow responses keep the requested cycle', async ({
  page,
}, testInfo) => {
  const [first, second] = await prepare(page, testInfo.project.name === 'mobile' ? 2032 : 2028);
  await page.goto('/workspace/enrollment?year=' + first.id);
  await expectYear(page, first);
  const endpoint = `**/api/v1/academic/years/${second.id}`;
  await page.route(endpoint, (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Ciclo temporalmente no disponible.' }),
    }),
  );
  await page.getByRole('combobox', { name: 'Ciclo', exact: true }).selectOption(second.id);
  await expect(page.getByRole('alert')).toContainText('Ciclo temporalmente no disponible.');
  await expect(page.locator('tbody')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Inscribir estudiante', exact: true })).toHaveCount(
    0,
  );
  await page.unroute(endpoint);
  await page.getByRole('button', { name: 'Reintentar', exact: true }).click();
  await expectYear(page, second);
  let release!: () => void, requested!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const arrived = new Promise<void>((resolve) => {
    requested = resolve;
  });
  await page.route(`**/api/v1/academic/years/${first.id}`, async (route) => {
    const response = await route.fetch();
    requested();
    await held;
    await route.fulfill({ response });
  });
  try {
    await page.getByRole('combobox', { name: 'Ciclo', exact: true }).selectOption(first.id);
    await arrived;
    await page.getByRole('combobox', { name: 'Ciclo', exact: true }).selectOption(second.id);
    await expectYear(page, second);
    const delivered = page.waitForResponse((response) =>
      response.url().endsWith('/academic/years/' + first.id),
    );
    release();
    await delivered;
    await page.waitForTimeout(150);
    await expectYear(page, second);
  } finally {
    release();
  }
  await page.goto('/workspace/enrollment?year=' + randomUUID());
  await expect(page.getByRole('alert')).toContainText('No encontramos el ciclo solicitado.');
  await expect(page.locator('tbody')).toHaveCount(0);
});
