import { expect, test } from '@playwright/test';
import { enterCredentials, login, year } from './support/school';

test.use({ trace: 'off' });
test('signing in after expiry returns to the selected cycle instead of resetting the context', async ({
  page,
}, testInfo) => {
  await login(page);
  const id = await year(page, testInfo.project.name === 'mobile' ? 2025 : 2024);
  const target = '/workspace?year=' + id;
  await page.goto(target);
  await expect(page.locator('.dash-context')).toBeVisible();
  // Expire the browser's real session while the page remains open, then trigger an API read.
  await page.context().clearCookies();
  await page.getByRole('button', { name: 'Actualizar', exact: true }).click();
  await expect(page).toHaveURL(/\/Identity\/Account\/Login\?/);
  expect(new URL(page.url()).searchParams.get('ReturnUrl')).toBe(target);
  await enterCredentials(page);
  await expect(page).toHaveURL(new RegExp('/workspace\\?year=' + id));
  await expect(page.locator('.dash-context')).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'Ciclo escolar', exact: true })).toHaveValue(id);
});
