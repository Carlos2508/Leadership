import { expect, test } from '@playwright/test';
import { login } from './support/school';

// Shortcuts used by the «Primeros pasos» guide on Inicio: they only open the right dialog, nothing is saved here.
test('first-steps shortcuts open the invitation and the own role change', async ({ page }) => {
  await login(page);
  await page.goto('/workspace/access?action=invite');
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(
    page.getByRole('dialog').getByLabel('Nombre de usuario', { exact: true }),
  ).toBeVisible();
  await page.keyboard.press('Escape');

  // The shortcut must also work when the current account is not on the first directory page.
  await page.route('**/api/v1/access/accounts?**', async (route) => {
    const url = new URL(route.request().url());
    if (url.searchParams.get('search')) return route.continue();
    const response = await route.fetch();
    const result = await response.json();
    result.items = result.items.filter(
      (x: { username: string }) => x.username !== process.env['SCHOOLPLATFORM_E2E_USERNAME'],
    );
    await route.fulfill({ response, json: result });
  });
  await page.goto('/workspace/access?action=my-role');
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(process.env['SCHOOLPLATFORM_E2E_USERNAME']!);
  await expect(dialog.getByRole('combobox', { name: /nuevo rol/i })).toHaveValue('administrator');
  await expect(dialog.getByLabel('Motivo del cambio')).toHaveValue(/Configuración inicial/);
  await page.keyboard.press('Escape');
});
