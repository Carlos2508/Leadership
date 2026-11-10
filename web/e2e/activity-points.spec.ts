import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { writeFile } from 'node:fs/promises';
import { courseFixture } from './support/school';

test('activity points keep their limits, save totals and print a populated gradebook', async ({
  page,
}, info) => {
  if (info.project.name === 'mobile') await page.setViewportSize({ width: 320, height: 640 });
  const f = await courseFixture(page, info.project.name === 'mobile' ? 2067 : 2066);
  await page.goto(`/workspace/courses/${f.course}/grades`);
  const dialog = page.locator('dialog[open]');
  const titles = [
    'Proyecto de investigación sobre la conservación del agua y su impacto en nuestra comunidad escolar',
    'Examen final del período',
  ];
  for (let i = 0; i < titles.length; i++) {
    await page.getByRole('button', { name: 'Agregar actividad', exact: true }).first().click();
    await dialog.getByLabel('Nombre de la actividad', { exact: true }).fill(titles[i]);
    await dialog
      .getByLabel('Punteo (puntos que vale)', { exact: true })
      .fill(i === 0 ? '40' : '60');
    await dialog.getByRole('button', { name: 'Guardar actividad', exact: true }).click();
    await expect(dialog).toHaveCount(0);
    await expect(
      page.getByRole('button', {
        name: titles[i] + ' ' + (i === 0 ? '40' : '60') + ' pts',
        exact: true,
      }),
    ).toBeVisible();
  }
  const rows = page.locator('.gb-table tbody tr');
  await rows.nth(0).locator('input.gb-input').nth(0).fill('30');
  await rows.nth(0).locator('input.gb-input').nth(1).fill('55');
  await rows.nth(1).locator('input.gb-input').nth(0).fill('N');
  await rows.nth(1).locator('input.gb-input').nth(1).fill('E');
  await expect(rows.nth(0).locator('.gb-total')).toHaveText('85');
  await expect(rows.nth(1).locator('.gb-total')).toHaveText('0');
  await expect(page.locator('.save-state')).toContainText('Guardado', { timeout: 10_000 });
  await page.reload();
  await expect(rows.nth(0).locator('input.gb-input').nth(0)).toHaveValue('30');
  await expect(rows.nth(0).locator('input.gb-input').nth(1)).toHaveValue('55');
  await expect(rows.nth(1).locator('input.gb-input').nth(0)).toHaveValue('N');
  await expect(rows.nth(1).locator('input.gb-input').nth(1)).toHaveValue('E');
  await expect(rows.nth(0).locator('.gb-total')).toHaveText('85');
  // Frozen name/total columns must leave the whole first editable cell exposed, even at 320 px.
  expect(
    await rows.nth(0).evaluate((row) => {
      const name = row.querySelector('.gb-student')!.getBoundingClientRect();
      const input = row.querySelector('input.gb-input')!.getBoundingClientRect();
      const total = row.querySelector('.gb-total')!.getBoundingClientRect();
      return input.left >= name.right - 1 && input.right <= total.left + 1;
    }),
  ).toBeTruthy();
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
  await page.screenshot({
    path: info.outputPath('populated-activity-gradebook.png'),
    fullPage: true,
  });
  const pdf = await page.request.get(
    (await page.getByRole('link', { name: 'Libreta en PDF' }).getAttribute('href'))!,
  );
  expect(pdf.status()).toBe(200);
  expect(pdf.headers()['content-type']).toContain('application/pdf');
  await writeFile(info.outputPath('activity-gradebook.pdf'), await pdf.body());
  await page.getByRole('button', { name: 'Agregar actividad', exact: true }).click();
  await dialog
    .getByLabel('Nombre de la actividad', { exact: true })
    .fill('Actividad que excede el límite');
  await dialog.getByLabel('Punteo (puntos que vale)', { exact: true }).fill('1');
  await dialog.getByRole('button', { name: 'Guardar actividad', exact: true }).click();
  await expect(dialog.getByRole('alert')).toContainText('100');
  await expect(page.locator('.gb-activity-head')).toHaveCount(2);
  await page.keyboard.press('Escape');
});
