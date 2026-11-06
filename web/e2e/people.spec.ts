import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

const reason = 'Autorización institucional sintética para prueba de navegador.';

async function signIn(page: Page, username: string, password: string): Promise<void> {
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Usuario', { exact: true }).fill(username);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(
    page.getByRole('heading', { name: /^(Buenos días|Buenas tardes|Buenas noches)/ }),
  ).toBeVisible();
}

async function post(page: Page, path: string, data: object) {
  const token = (await (await page.request.get('/api/v1/antiforgery')).json()).token as string;
  return page.request.post(path, { data, headers: { 'X-XSRF-TOKEN': token } });
}

async function expectAccessible(page: Page): Promise<void> {
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy();
}

test('register a family, grant explicit faculties and show linked students to the guardian account', async ({
  page,
  browser,
}, testInfo) => {
  const owner = process.env['SCHOOLPLATFORM_E2E_USERNAME']!;
  const ownerPassword = process.env['SCHOOLPLATFORM_E2E_PASSWORD']!;
  const suffix = randomUUID().slice(0, 6);
  const studentCode = 'E2E-' + suffix.toUpperCase();
  const family = 'Sintético ' + suffix;
  const guardianUser = 'guardian-' + suffix;
  const guardianPassword = 'Guardian-Synthetic!12345';

  await signIn(page, owner, ownerPassword);
  // The provisioned owner only manages accounts; grant the institutional profile once, then sign in again.
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('people.manage')) {
    const accounts = await (
      await page.request.get('/api/v1/access/accounts?search=' + encodeURIComponent(owner))
    ).json();
    const me = accounts.items.find((x: { id: string }) => x.id === session.userId);
    const changed = await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
      version: me.version,
      reason,
      profile: 'administrator',
    });
    expect(changed.status()).toBe(204);
    await signIn(page, owner, ownerPassword);
  }

  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('home.png'), fullPage: true });
  if (testInfo.project.name === 'mobile')
    await page.getByRole('button', { name: 'Abrir menú' }).click();
  await page
    .getByRole('navigation', { name: 'Navegación principal' })
    .getByRole('link', { name: 'Estudiantes', exact: true })
    .click();
  await expect(page.getByRole('heading', { name: 'Estudiantes', level: 1 })).toBeVisible();
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('people-directory.png'), fullPage: true });

  // Student
  await page.getByRole('button', { name: /Registrar (estudiante|padre o encargado)/ }).click();
  await page.getByLabel('Nombres', { exact: true }).fill('Valeria');
  await page.getByLabel('Apellidos', { exact: true }).fill(family);
  await page.getByText('Contacto y otros datos (opcionales)', { exact: true }).click();
  await page.getByLabel(/^Teléfono/).fill('+502 5555-0101');

  await page.getByLabel('Código de estudiante').fill(studentCode);
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('person-dialog.png'), fullPage: true });
  await page.getByRole('button', { name: 'Registrar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Valeria ' + family, level: 1 })).toBeVisible();
  await expect(page.getByText('Estudiante · ' + studentCode)).toBeVisible();

  // Guardian
  await page.getByRole('link', { name: 'Volver al listado' }).click();
  await page.goto('/workspace/parents');
  await page.getByRole('button', { name: /Registrar (estudiante|padre o encargado)/ }).click();
  await page.getByLabel('Nombres', { exact: true }).fill('Andrea');
  await page.getByLabel('Apellidos', { exact: true }).fill(family);

  await page.getByRole('button', { name: 'Registrar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Andrea ' + family, level: 1 })).toBeVisible();
  const guardianUrl = page.url();

  // Relationship with one explicit faculty
  await page.getByRole('button', { name: 'Vincular estudiante' }).click();
  await page.getByLabel('Buscar estudiante').fill(studentCode);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.getByRole('radio', { name: new RegExp('Valeria ' + family) }).check();
  await page.getByLabel('Parentesco o vínculo').selectOption('Mother');
  await page.getByRole('checkbox', { name: /Consultar información académica/ }).check();
  await page.getByLabel('Motivo del cambio').fill(reason);
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('relationship-dialog.png'), fullPage: true });
  await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.locator('.sp-toast')).toContainText('El vínculo quedó registrado');
  const row = page.locator('.relationship-row').filter({ hasText: 'Valeria ' + family });
  await expect(row).toContainText('Madre');
  await expect(row).toContainText('Información académica');

  // Account for the guardian: invitation, activation and link
  const invited = await post(page, '/api/v1/access/invitations', {
    username: guardianUser,
    profile: 'basic',
    reason,
  });
  expect(invited.status()).toBe(200);
  const code = (await invited.json()).code as string;
  const guardianContext = await browser.newContext({ baseURL: new URL(page.url()).origin });
  const guardian = await guardianContext.newPage();
  await guardian.goto('/Identity/Account/Activate');
  await guardian.getByLabel('Código de acceso', { exact: true }).fill(code);
  await guardian.getByLabel('Nueva contraseña', { exact: true }).fill(guardianPassword);
  await guardian.getByLabel('Confirmar contraseña', { exact: true }).fill(guardianPassword);
  await guardian.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await expect(guardian.getByRole('heading', { name: 'Tu acceso está listo' })).toBeVisible();

  await page.goto(guardianUrl);
  await page.getByRole('button', { name: 'Vincular cuenta' }).click();
  await page.getByLabel('Buscar cuenta').fill(guardianUser);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.getByRole('radio', { name: new RegExp(guardianUser) }).check();
  await page.getByLabel('Motivo del cambio').fill(reason);
  await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.locator('.sp-toast')).toContainText('La cuenta quedó vinculada');
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('person-detail.png'), fullPage: true });

  await signIn(guardian, guardianUser, guardianPassword);
  await expect(guardian.getByRole('heading', { name: 'Andrea ' + family })).toBeVisible();
  const linked = guardian.locator('.relationship-row').filter({ hasText: 'Valeria ' + family });
  await expect(linked).toContainText('Información académica');
  await expect(guardian.getByRole('link', { name: 'Estudiantes', exact: true })).toHaveCount(0);
  expect((await guardian.request.get('/api/v1/people')).status()).toBe(403);
  await expectAccessible(guardian);
  await guardian.screenshot({ path: testInfo.outputPath('guardian-home.png'), fullPage: true });
  await guardianContext.close();
});
