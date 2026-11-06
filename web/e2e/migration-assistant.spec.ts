import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { login, post, year } from './support/school';
test.use({ actionTimeout: 15_000 });

test('local assistant detects the source, reviews by grade, backs up and imports with inactive accounts', async ({
  page,
}, testInfo) => {
  test.skip(
    process.env['SCHOOLPLATFORM_E2E_MIGRATION'] !== '1',
    'Requires the isolated MariaDB fixture; run Test-LegacyExtractor.ps1 -RunAssistantE2E.',
  );
  test.setTimeout(120_000);
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await login(page);
  const yearId = await year(page, 2027);
  const grade = await post(page, '/api/v1/academic/grades', {
    code: 'AS5',
    name: 'Quinto sintético',
    stage: 'Primaria',
    sequence: 5,
  });
  await page.goto('/workspace/migration');
  await post(page, `/api/v1/academic/years/${yearId}/sections`, {
    gradeLevelId: grade.id,
    name: 'A',
    shift: null,
    capacity: 30,
  });
  await page.reload();
  await expect(
    page.getByRole('heading', { name: 'Migrar estudiantes', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Buscar plataforma anterior' }).click();
  await expect(page.getByLabel('Ciclo de la plataforma anterior')).toBeVisible();
  await page.getByLabel('Ciclo de la plataforma anterior').selectOption('42');
  await page.getByRole('combobox', { name: 'Ciclo de destino', exact: true }).selectOption(yearId);
  await page.screenshot({ path: testInfo.outputPath('01-source.png'), fullPage: true });
  await page.getByRole('button', { name: 'Preparar estudiantes y familias' }).click();
  await expect(page.getByRole('heading', { name: 'Revisa lo importante' })).toBeVisible();
  await page.getByRole('combobox', { name: /^Cuarto sintético/ }).selectOption(grade.id);
  await page
    .getByRole('combobox', { name: 'Sección para Cuarto sintético' })
    .selectOption({ label: 'A' });
  await page.getByLabel('Ver solo propuestas que revisar').uncheck();
  await expect(page.getByLabel('Código', { exact: true }).first()).toHaveValue('000001');
  await page.getByRole('button', { name: 'Guardar y continuar después' }).click();
  await expect(page.locator('.sp-toast')).toContainText('Revisión guardada');
  await page.reload();
  await page.getByRole('button', { name: /Abrir revisión de Recorridos 2027/ }).click();
  await expect(
    page.getByRole('combobox', { name: /^Cuarto sintético/ }).locator('option:checked'),
  ).toHaveText('Quinto sintético');
  await expect(
    page.getByRole('combobox', { name: 'Sección para Cuarto sintético' }).locator('option:checked'),
  ).toHaveText('A');
  await page.screenshot({ path: testInfo.outputPath('02-review.png'), fullPage: true });
  await expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
    .toBeTruthy();
  await page
    .getByLabel(
      'Confirmo los estudiantes que continúan, sus grados y los nombres y vínculos propuestos.',
    )
    .check();
  await page.getByRole('button', { name: 'Comprobar y continuar' }).click();
  await expect(page.getByRole('heading', { name: 'Listo para Recorridos 2027' })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('03-ready.png'), fullPage: true });
  await page.getByRole('button', { name: 'Importar estudiantes', exact: true }).click();
  await page.getByRole('button', { name: 'Sí, importar estudiantes' }).click();
  await expect(page.getByRole('heading', { name: '3 estudiantes en Recorridos 2027' })).toBeVisible(
    { timeout: 60_000 },
  );
  await expect(page.getByText('Respaldo creado y verificado.', { exact: true })).toBeVisible();
  await expect(page.getByText('Los accesos siguen inactivos.', { exact: false })).toBeVisible();
  await expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('04-done.png'), fullPage: true });
  const enrollments = await (await page.request.get(`/api/v1/years/${yearId}/enrollments`)).json();
  expect(enrollments.total).toBe(3);
  expect(enrollments.items.every((e: { section: string }) => e.section === 'A')).toBeTruthy();
  const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
  expect(accounts.items.filter((a: { status: string }) => a.status === 'Pending')).toHaveLength(5);
  await page.getByRole('button', { name: 'Volver a verificar' }).click();
  await expect(
    page.getByRole('heading', { name: '3 estudiantes en Recorridos 2027' }),
  ).toBeVisible();
  expect(errors).toEqual([]);
});
