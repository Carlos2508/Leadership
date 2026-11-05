import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

test('manage an invitation, activate without email, recover access and inspect durable audit', async ({
  page,
  browser,
}, testInfo) => {
  const username = 'browser-' + randomUUID().slice(0, 8);
  const password = 'Browser-Synthetic!12345';
  const recovered = 'Recovered-Synthetic!67890';
  await page.goto('/Identity/Account/Login');
  await page
    .getByLabel('Usuario', { exact: true })
    .fill(process.env['SCHOOLPLATFORM_E2E_USERNAME']!);
  await page
    .getByLabel('Contraseña', { exact: true })
    .fill(process.env['SCHOOLPLATFORM_E2E_PASSWORD']!);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(page.getByRole('heading', { name: /bienvenida/i })).toBeVisible();
  await page.getByRole('link', { name: 'Administrar cuentas' }).click();
  await expect(page.getByRole('heading', { name: 'Cuentas y accesos' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Invitar cuenta' })).toBeEnabled();
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('accounts.png'), fullPage: true });
  await page.getByRole('button', { name: 'Invitar cuenta' }).click();
  await page.getByLabel('Nombre de usuario', { exact: true }).fill(username);
  await page
    .getByLabel('Motivo de la invitación')
    .fill('Autorización institucional sintética para prueba de navegador.');
  await page.screenshot({ path: testInfo.outputPath('invite-dialog.png'), fullPage: true });
  const dialogBounds = await page.getByRole('dialog').boundingBox();
  expect(dialogBounds!.x).toBeGreaterThanOrEqual(0);
  expect(dialogBounds!.x + dialogBounds!.width).toBeLessThanOrEqual(page.viewportSize()!.width);
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.getByRole('button', { name: 'Crear invitación' }).click();
  const codeInput = page.getByLabel('Código de un solo uso', { exact: true });
  await expect(codeInput).toHaveValue(/^[a-f0-9]{32}\./);
  const activationCode = await codeInput.inputValue();
  await page.getByRole('button', { name: 'Ya lo guardé' }).click();
  await expect(codeInput).toHaveCount(0);

  const studentContext = await browser.newContext({ baseURL: new URL(page.url()).origin });
  const student = await studentContext.newPage();
  await student.goto('/Identity/Account/Activate');
  await student.getByLabel('Código de acceso', { exact: true }).fill(activationCode);
  await student.getByLabel('Nueva contraseña', { exact: true }).fill(password);
  await student.getByLabel('Confirmar contraseña', { exact: true }).fill(password);
  await student.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await expect(student.getByRole('heading', { name: 'Tu acceso está listo' })).toBeVisible();
  await student.getByRole('link', { name: 'Ingresar a mi espacio' }).click();
  await student.getByLabel('Usuario', { exact: true }).fill(username);
  await student.getByLabel('Contraseña', { exact: true }).fill(password);
  await student.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(student.getByRole('heading', { name: /bienvenida/i })).toBeVisible();
  await expect(student.getByRole('link', { name: 'Administrar cuentas' })).toHaveCount(0);
  expect((await student.request.get('/api/v1/access/accounts')).status()).toBe(403);
  expect((await student.request.get('/api/v1/audit')).status()).toBe(403);

  await page.getByRole('searchbox').fill(username);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await expect(page.locator('tbody')).toContainText('Activa');
  await page.getByRole('button', { name: 'Gestionar ' + username, exact: true }).click();
  await page.getByRole('combobox', { name: 'Acción', exact: true }).selectOption('issue');
  await page.getByRole('checkbox').check();
  await page
    .getByLabel('Cómo verificaste la identidad')
    .fill('Titular verificado presencialmente con el procedimiento sintético.');
  await page.getByRole('button', { name: 'Generar código de acceso' }).click();
  await expect(codeInput).toHaveValue(/^[a-f0-9]{32}\./);
  const recoveryCode = await codeInput.inputValue();
  await page.getByRole('button', { name: 'Ya lo guardé' }).click();
  const recoveryPage = await studentContext.newPage();
  await recoveryPage.goto('/Identity/Account/Activate');
  await recoveryPage.getByLabel('Código de acceso', { exact: true }).fill(recoveryCode);
  await recoveryPage.getByLabel('Nueva contraseña', { exact: true }).fill(recovered);
  await recoveryPage.getByLabel('Confirmar contraseña', { exact: true }).fill(recovered);
  await recoveryPage.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await expect(recoveryPage.getByRole('heading', { name: 'Tu acceso está listo' })).toBeVisible();
  expect((await student.request.get('/api/v1/session')).status()).toBe(401);
  await studentContext.close();

  await page.goto('/workspace/audit');
  await expect(page.getByRole('heading', { name: 'Auditoría', level: 1 })).toBeVisible();
  await expect(page.locator('tbody')).toContainText('Contraseña recuperada');
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy();
  await page.screenshot({ path: testInfo.outputPath('audit.png'), fullPage: true });
});

test('activation form validates passwords and handles an invalid one-use code', async ({
  page,
}) => {
  await page.goto('/Identity/Account/Activate');
  await expect(page.getByRole('heading', { name: 'Prepara tu acceso.' })).toBeVisible();
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.getByLabel('Código de acceso', { exact: true }).fill('invalid-synthetic-code');
  await page.getByLabel('Nueva contraseña', { exact: true }).fill('Synthetic!Password123');
  await page.getByLabel('Confirmar contraseña', { exact: true }).fill('Synthetic!Password123');
  await page.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await expect(page.getByRole('alert')).toContainText('El código no es válido');
  await expect(page.getByLabel('Código de acceso', { exact: true })).toHaveValue('');
});
