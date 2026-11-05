import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('welcome loads configured branding and opens the real login form', async ({
  page,
}, testInfo) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/');
  await expect(page).toHaveTitle('Colegio Leadership Mixco');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('¡Piensa en grande!');
  await expect(page.getByRole('img', { name: 'Colegio Leadership Mixco' })).toBeVisible();
  await expect(page.locator('.welcome-main')).toHaveCSS('display', 'grid');
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy();
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('welcome.png'), fullPage: true });
  await expect(page.locator('#contacto')).toContainText('Cómo llegar');
  await page.getByRole('link', { name: 'Ingresar a mi espacio' }).first().click();
  await expect(page.getByLabel('Usuario', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Contraseña', { exact: true })).toBeVisible();
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy();
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('login.png'), fullPage: true });
  expect(errors).toEqual([]);
});

test('sample dashboard renders and student filters combine without changing records', async ({
  page,
}, testInfo) => {
  await page.goto('/preview');
  await expect(page.getByRole('heading', { name: 'Resumen del colegio' })).toBeVisible();
  await expect(page.locator('.sidebar .brand img')).toHaveCSS('width', '46px');
  await expect(page.getByRole('img', { name: /Asistencia ficticia/ })).toBeVisible();
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy();
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('dashboard.png'), fullPage: true });
  if (testInfo.project.name === 'mobile') {
    await page.getByRole('button', { name: 'Abrir menú' }).click();
    await expect(page.getByRole('navigation').getByRole('link').first()).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('button', { name: 'Abrir menú' })).toBeFocused();
    await expect(page.getByRole('button', { name: 'Abrir menú' })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
    await page.getByRole('button', { name: 'Abrir menú' }).click();
  }
  await page
    .getByRole('navigation')
    .getByRole('link', { name: 'Estudiantes', exact: true })
    .click();
  await expect(page.locator('tbody tr')).toHaveCount(6);
  await page.getByRole('searchbox').fill('sofia');
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await expect(page.locator('tbody')).toContainText('Ana Sofía Morales');
  await page.getByRole('combobox', { name: 'Grado', exact: true }).selectOption('2.º básico');
  await expect(page.getByRole('heading', { name: 'No encontramos coincidencias' })).toBeVisible();
  await page.getByRole('button', { name: 'Limpiar filtros' }).click();
  await page.getByRole('combobox', { name: 'Estado', exact: true }).selectOption('Pendiente');
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await expect(page.locator('tbody')).toContainText('Mateo García');
  await page.getByRole('button', { name: 'Limpiar filtros' }).click();
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy();
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('students.png'), fullPage: true });
});

test('unauthenticated workspace returns to login', async ({ page }) => {
  await page.goto('/workspace');
  await expect(page).toHaveURL(/\/Identity\/Account\/Login/);
  await expect(page.getByRole('button', { name: 'Ingresar a mi espacio' })).toBeVisible();
});

test('bootstrap failure shows an actionable retry instead of a blank page', async ({ page }) => {
  await page.route('**/api/v1/bootstrap', (route) => route.fulfill({ status: 503, body: '{}' }));
  await page.goto('/');
  await expect(page.getByRole('alert')).toContainText('No pudimos cargar la plataforma');
  await expect(page.getByRole('button', { name: 'Volver a intentar' })).toBeVisible();
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
});
