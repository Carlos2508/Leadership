import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { courseFixture, post } from './support/school';

async function changeDate(page: Page, value: string) {
  await page.getByLabel('Fecha', { exact: true }).fill(value);
  await page.getByLabel('Fecha', { exact: true }).blur();
}
async function open(page: Page, course: string, date: string) {
  await page.goto(`/workspace/courses/${course}/attendance`);
  await expect(page.getByRole('radio', { name: 'Presente', exact: true })).toHaveCount(2);
  await changeDate(page, date);
  await expect(page.getByLabel('Fecha', { exact: true })).toHaveValue(date);
  await expect(page.getByRole('radio', { name: 'Presente', exact: true })).toHaveCount(2);
}
test('attendance keeps unsaved marks when a date change is cancelled and restores saved days from history', async ({
  page,
}, testInfo) => {
  const number = testInfo.project.name === 'mobile' ? 2025 : 2024;
  const { course } = await courseFixture(page, number);
  const date = `${number}-02-16`;
  const next = `${number}-02-17`;
  await open(page, course, date);
  await page.getByRole('radio', { name: 'Ausente', exact: true }).first().check();
  await changeDate(page, next);
  await expect(page.getByRole('dialog')).toContainText('Asistencia sin guardar');
  await page.getByRole('button', { name: 'Seguir editando', exact: true }).click();
  await expect(page.getByLabel('Fecha', { exact: true })).toHaveValue(date);
  await expect(page.getByRole('radio', { name: 'Ausente', exact: true }).first()).toBeChecked();
  await expect(page.locator('.attendance-counts')).toContainText('Cambios sin guardar');
  await page.getByRole('button', { name: 'Guardar asistencia', exact: true }).click();
  await expect(page.locator('.attendance-counts')).toContainText('Registrada');
  await expect(
    page.getByRole('button', { name: 'Guardar asistencia', exact: true }),
  ).toBeDisabled();
  await page.getByRole('button', { name: 'Día siguiente', exact: true }).click();
  await expect(page.getByLabel('Fecha', { exact: true })).toHaveValue(next);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.locator('.attendance-counts')).toContainText('Sin registrar');
  await page.goBack();
  await expect(page.getByLabel('Fecha', { exact: true })).toHaveValue(date);
  await expect(page.getByRole('radio', { name: 'Ausente', exact: true }).first()).toBeChecked();
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('attendance-saved.png'), fullPage: true });
  if (testInfo.project.name === 'desktop') {
    for (const width of [320, 768, 1920]) {
      await page.setViewportSize({ width, height: 900 });
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
      ).toBeTruthy();
      const dateField = await page.getByLabel('Fecha', { exact: true }).boundingBox();
      expect(dateField!.width).toBeGreaterThan(100);
      expect(
        (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
          .violations,
      ).toEqual([]);
      await page.screenshot({
        path: testInfo.outputPath(`attendance-${width}.png`),
        fullPage: true,
      });
    }
  }
});
test('a failed attendance date never displays another day and slow responses cannot overwrite the selected day', async ({
  page,
}, testInfo) => {
  const number = testInfo.project.name === 'mobile' ? 2025 : 2024;
  const { course, students } = await courseFixture(page, number);
  const first = `${number}-02-16`,
    second = `${number}-02-17`;
  for (const [date, status] of [
    [first, 'Absent'],
    [second, 'Late'],
  ]) {
    await post(page, `/api/v1/courses/${course}/attendance`, {
      date,
      entries: students.map((studentId) => ({ studentId, status, note: null, version: null })),
    });
  }
  await open(page, course, first);
  const failed = `**/api/v1/courses/${course}/attendance?date=${second}`;
  await page.route(failed, (route) =>
    route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Asistencia temporalmente no disponible.' }),
    }),
  );
  await changeDate(page, second);
  await expect(page.getByRole('alert')).toContainText('Asistencia temporalmente no disponible.');
  await expect(page.locator('.attendance-list')).toHaveCount(0);
  await page.unroute(failed);
  await page.getByRole('button', { name: 'Reintentar asistencia', exact: true }).click();
  await expect(page.getByRole('radio', { name: 'Tarde', exact: true }).first()).toBeChecked();
  let release!: () => void, requested!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const arrived = new Promise<void>((resolve) => {
    requested = resolve;
  });
  await page.route(`**/api/v1/courses/${course}/attendance?date=${first}`, async (route) => {
    const response = await route.fetch();
    requested();
    await held;
    await route.fulfill({ response });
  });
  try {
    await changeDate(page, first);
    await arrived;
    await changeDate(page, second);
    await expect(page.getByRole('radio', { name: 'Tarde', exact: true }).first()).toBeChecked();
    const delivered = page.waitForResponse((response) => response.url().endsWith('date=' + first));
    release();
    await delivered;
    await page.waitForTimeout(150);
    await expect(page.getByLabel('Fecha', { exact: true })).toHaveValue(second);
    await expect(page.getByRole('radio', { name: 'Tarde', exact: true }).first()).toBeChecked();
  } finally {
    release();
  }
});
test('failed attendance saves preserve marks and notes; leaving waits for the confirmed save', async ({
  page,
}, testInfo) => {
  const number = testInfo.project.name === 'mobile' ? 2025 : 2024;
  const { course } = await courseFixture(page, number);
  const date = `${number}-02-16`;
  await open(page, course, date);
  const endpoint = `**/api/v1/courses/${course}/attendance`;
  await page.route(endpoint, (route) =>
    route.request().method() === 'POST'
      ? route.fulfill({
          status: 503,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'No se pudo guardar la asistencia.' }),
        })
      : route.continue(),
  );
  await page.getByRole('radio', { name: 'Tarde', exact: true }).first().check();
  const note = page.getByRole('textbox', { name: /^Observación de Ana/ });
  await note.fill('Llegó con autorización sintética.');
  await page.getByRole('button', { name: 'Guardar asistencia', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('No se pudo guardar la asistencia.');
  await expect(note).toHaveValue('Llegó con autorización sintética.');
  await page.unroute(endpoint);
  let release!: () => void, requested!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  const arrived = new Promise<void>((resolve) => {
    requested = resolve;
  });
  await page.route(endpoint, async (route) => {
    if (route.request().method() !== 'POST') return route.continue();
    const response = await route.fetch();
    requested();
    await held;
    await route.fulfill({ response });
  });
  try {
    await page.getByRole('button', { name: 'Guardar asistencia', exact: true }).click();
    await arrived;
    await page.getByRole('link', { name: 'Volver a la lista', exact: true }).click();
    await expect(page).toHaveURL(/\/attendance/);
    release();
    await expect(page).toHaveURL(new RegExp(`/workspace/courses/${course}$`));
    const saved = await (
      await page.request.get(`/api/v1/courses/${course}/attendance?date=${date}`)
    ).json();
    expect(saved.rows[0].status).toBe('Late');
    expect(saved.rows[0].note).toBe('Llegó con autorización sintética.');
  } finally {
    release();
  }
});
