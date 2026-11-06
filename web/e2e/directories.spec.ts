import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

test('school modules keep their own profiles, clear filters and work across viewport sizes', async ({
  page,
}, testInfo) => {
  const owner = process.env['SCHOOLPLATFORM_E2E_USERNAME']!;
  const password = process.env['SCHOOLPLATFORM_E2E_PASSWORD']!;
  const signIn = async () => {
    await page.goto('/Identity/Account/Login');
    await page.getByLabel('Usuario', { exact: true }).fill(owner);
    await page.getByLabel('Contraseña', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
    await expect(
      page.getByRole('heading', { name: /^¡Hola,/ }),
    ).toBeVisible();
  };
  const post = async (path: string, data: object) => {
    const token = (await (await page.request.get('/api/v1/antiforgery')).json()).token;
    const response = await page.request.post(path, { data, headers: { 'X-XSRF-TOKEN': token } });
    expect(response.status()).toBeLessThan(300);
  };
  await signIn();
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('people.manage')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((x: { id: string }) => x.id === session.userId);
    await post(`/api/v1/access/accounts/${me.id}/profile`, {
      version: me.version,
      reason: 'Prueba sintética de módulos escolares.',
      profile: 'administrator',
    });
    await signIn();
  }
  const suffix = randomUUID().slice(0, 8);
  const familyNames = 'Directorio ' + suffix;
  for (const role of ['student', 'guardian', 'staff']) {
    await post('/api/v1/people', {
      givenNames: role,
      familyNames,
      studentCode: role === 'student' ? 'DIR-' + suffix : null,
      isGuardian: role === 'guardian',
      staffPosition: role === 'staff' ? 'Docente' : null,
      contacts: [],
    });
  }
  for (const [route, title, expected] of [
    ['students', 'Estudiantes', 'student'],
    ['parents', 'Padres y encargados', 'guardian'],
    ['teachers', 'Docentes y personal', 'staff'],
  ]) {
    await page.goto('/workspace/' + route);
    await expect(page.getByRole('heading', { level: 1, name: title, exact: true })).toBeVisible();
    await page.getByRole('searchbox').fill(familyNames);
    await page.getByRole('button', { name: 'Buscar', exact: true }).click();
    await expect(page.locator('tbody tr')).toHaveCount(1);
    await expect(page.locator('tbody')).toContainText(expected);
    const cleared = page.waitForResponse(
      (r) => r.url().includes('/api/v1/people?') && r.request().method() === 'GET',
    );
    await page.getByRole('button', { name: 'Limpiar filtros' }).click();
    await expect(page.getByRole('searchbox')).toHaveValue('');
    const query = new URLSearchParams(new URL((await cleared).url()).search);
    expect(query.get('role')).toBe(expected);
    expect(
      (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
        .violations,
    ).toEqual([]);
    await page.screenshot({ path: testInfo.outputPath(route + '.png'), fullPage: true });
  }
  for (const width of [320, 768, 1024, 1920]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.goto('/workspace/students');
    await expect(
      page.getByRole('heading', { level: 1, name: 'Estudiantes', exact: true }),
    ).toBeVisible();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
      `overflow at ${width}`,
    ).toBeTruthy();
    await page.getByRole('button', { name: 'Registrar estudiante' }).click();
    await expect(page.locator('input[name=isStudent]')).toBeChecked();
    await expect(page.getByLabel('Código de estudiante')).toBeVisible();
    expect(
      (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
        .violations,
    ).toEqual([]);
    expect(
      await page
        .getByRole('dialog')
        .evaluate((el) => el.getBoundingClientRect().right <= innerWidth),
    ).toBeTruthy();
    await page.screenshot({
      path: testInfo.outputPath('register-' + width + '.png'),
      fullPage: true,
    });
    await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
  }
  await expect(page.getByRole('link', { name: 'Lector de entrada', exact: true })).toBeHidden();
  await page.locator('summary').filter({ hasText: 'Portería' }).click();
  await page.getByRole('link', { name: 'Lector de entrada', exact: true }).click();
  await expect(page).toHaveURL(/\/workspace\/gate$/);
  await expect(page.locator('.nav-section[open]')).toContainText('Portería');
});
