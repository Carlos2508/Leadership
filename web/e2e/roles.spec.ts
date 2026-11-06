import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

test('custom access roles accept all permissions and update assigned accounts through the reviewed editor', async ({
  page,
}, testInfo) => {
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
  await page.goto('/workspace/roles');
  await expect(page.getByRole('heading', { name: 'Roles y permisos', exact: true })).toBeVisible();
  const suffix = randomUUID().slice(0, 6);
  const label = 'Secretaría ' + suffix;
  await page.getByRole('button', { name: 'Crear rol', exact: true }).click();
  await page.getByLabel('Nombre del rol', { exact: true }).fill(label);
  await page.getByRole('button', { name: 'Seleccionar todos', exact: true }).click();
  await expect(page.getByRole('checkbox', { checked: true })).toHaveCount(15);
  await page
    .getByLabel('Motivo del cambio')
    .fill('Permisos completos aprobados para el rol sintético.');
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('role-editor.png'), fullPage: true });
  await page.getByRole('button', { name: 'Guardar rol', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  const row = page.getByRole('row').filter({ hasText: label });
  await expect(row).toContainText('15 de 15');
  const roles = await (await page.request.get('/api/v1/access/roles')).json();
  const role = roles.find((r: { label: string }) => r.label === label);
  const { token } = await (await page.request.get('/api/v1/antiforgery')).json();
  const invitation = await page.request.post('/api/v1/access/invitations', {
    data: {
      username: 'roles-' + suffix,
      profile: role.key,
      reason: 'Asignación sintética del nuevo rol de acceso.',
    },
    headers: { 'X-XSRF-TOKEN': token },
  });
  expect(invitation.status()).toBe(200);
  await page.goto('/workspace/access');
  await page.getByRole('searchbox').fill('roles-' + suffix);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await expect(page.locator('tbody')).toContainText(label);
  await page.goto('/workspace/roles');
  await page
    .getByRole('row')
    .filter({ hasText: label })
    .getByRole('button', { name: new RegExp('Editar') })
    .click();
  await page.getByRole('button', { name: 'Quitar selección', exact: true }).click();
  // Wait for the bulk change to reach the controls before choosing a permission again.
  await expect(page.getByRole('dialog').getByRole('checkbox', { checked: true })).toHaveCount(1); // Rol activo only.
  await page
    .getByRole('checkbox', { name: 'Consultar estudiantes, padres y personal', exact: true })
    .check();
  await expect(page.getByRole('dialog')).toContainText('1 permisos seleccionados.');
  await page
    .getByLabel('Motivo del cambio')
    .fill('Reducir este rol sintético a la consulta de fichas.');
  await page.getByRole('button', { name: 'Guardar rol', exact: true }).click();
  await expect(page.locator('.sp-alert')).toContainText('1 cuenta');
  await page.getByRole('button', { name: 'Actualizar permisos', exact: true }).click();
  await expect(page.locator('dialog.account-dialog')).toBeHidden();
  await expect(page.getByRole('row').filter({ hasText: label })).toContainText('1 de 15');
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
  await page.screenshot({ path: testInfo.outputPath('roles.png'), fullPage: true });
});
