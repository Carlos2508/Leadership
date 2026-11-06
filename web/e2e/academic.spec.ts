import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

const reason = 'Autorización institucional sintética para prueba de navegador.';

async function signIn(page: Page): Promise<void> {
  const login = async () => {
    await page.goto('/Identity/Account/Login');
    await page
      .getByLabel('Usuario', { exact: true })
      .fill(process.env['SCHOOLPLATFORM_E2E_USERNAME']!);
    await page
      .getByLabel('Contraseña', { exact: true })
      .fill(process.env['SCHOOLPLATFORM_E2E_PASSWORD']!);
    await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
    await expect(page.getByRole('heading', { name: /^¡Hola,/ })).toBeVisible();
  };
  await login();
  const session = await (await page.request.get('/api/v1/session')).json();
  if (session.permissions.includes('enrollment.manage')) return;
  const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
  const me = accounts.items.find((x: { id: string }) => x.id === session.userId);
  expect(
    (
      await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
        version: me.version,
        reason,
        profile: 'administrator',
      })
    ).status(),
  ).toBe(204);
  await login();
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

test('prepare a school year, enroll without inventing a section and keep placement history', async ({
  page,
}, testInfo) => {
  const suffix = randomUUID().slice(0, 5).toUpperCase();
  // Years cannot overlap; each browser project uses its own far-future synthetic year.
  const calendarYear =
    2040 + testInfo.repeatEachIndex * 2 + (testInfo.project.name === 'mobile' ? 1 : 0);
  const label = `${calendarYear}-${suffix}`;
  const gradeName = 'Grado E2E ' + suffix;
  const studentCode = 'MAT-' + suffix;
  await signIn(page);

  const student = await post(page, '/api/v1/people', {
    givenNames: 'Mateo',
    familyNames: 'Sintético ' + suffix,
    contacts: [],
    studentCode,
    isGuardian: false,
    confirmDuplicate: true,
  });
  expect(student.status()).toBe(200);
  const studentId = (await student.json()).id as string;

  await page.goto('/workspace/academic');
  await expect(page.getByRole('heading', { name: 'Ciclos y grados', level: 1 })).toBeVisible();

  // Grade catalog
  await page.getByRole('button', { name: '+ Nuevo' }).click();
  await page.getByLabel('Código').fill('G' + suffix);
  await page.getByLabel('Nombre', { exact: true }).fill(gradeName);
  await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.locator('.sp-toast')).toContainText('El grado se guardó');

  // Year in draft: the new-cycle dialog generates the periods; the copy shortcuts are left off (isolated test data).
  await page.getByRole('button', { name: 'Nuevo ciclo' }).click();
  await expect(page.getByRole('heading', { name: 'Nuevo ciclo escolar' })).toBeVisible();
  await page.getByLabel('Nombre del ciclo').fill(label);
  await page.getByLabel('Inicio', { exact: true }).fill(`${calendarYear}-01-15`);
  await page.getByLabel('Fin', { exact: true }).fill(`${calendarYear}-10-30`);
  const dialog = page.getByRole('dialog');
  for (const name of [/^Copiar las secciones/, /^Copiar los cursos/]) {
    const option = dialog.getByRole('checkbox', { name });
    if (await option.count()) await option.uncheck();
  }
  await dialog.getByLabel('Cantidad').selectOption({ label: '4 bimestres' });
  await expect(dialog.locator('.period-preview li')).toHaveCount(4);
  await expectAccessible(page);
  await dialog.getByRole('button', { name: 'Crear ciclo', exact: true }).click();
  await expect(page.locator('.sp-toast')).toContainText('4 bimestres generados');
  await expect(page.getByRole('heading', { name: 'Ciclo ' + label, exact: true })).toBeVisible();
  await expect(page.locator('.period-list li')).toHaveCount(4);
  await expect(page.locator('.period-list')).toContainText(`30/10/${calendarYear}`);
  await expect(page.locator('.setup-steps li').first()).toHaveClass(/done/);
  // A generated period can still be adjusted while the calendar is open.
  await page.getByRole('button', { name: 'Editar Bimestre 1' }).click();
  await page.getByLabel('Fin', { exact: true }).fill(`${calendarYear}-03-20`);
  await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.locator('.period-list')).toContainText(`20/03/${calendarYear}`);
  await page.getByRole('button', { name: `+ Sección para ${gradeName}` }).click();
  await page.getByLabel('Nombre', { exact: true }).fill('A');
  await page.getByLabel(/^Cupo/).fill('30');
  await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.locator('.sp-toast')).toContainText('La sección se guardó');
  await page.getByRole('button', { name: 'Abrir inscripciones' }).first().click();
  await expect(page.getByLabel('Motivo del cambio')).toHaveValue(/Apertura de inscripciones/);
  await page.getByLabel('Motivo del cambio').fill(reason);
  await page.getByRole('dialog').getByRole('button', { name: 'Abrir inscripciones' }).click();
  await expect(page.locator('.sp-toast')).toContainText('El estado del ciclo cambió');
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('academic-structure.png'), fullPage: true });

  // Enrollment with section, then a dated move to "no section"
  await page.getByRole('link', { name: 'Ver inscripciones' }).click();
  await expect(page.getByRole('heading', { name: 'Inscripciones', level: 1 })).toBeVisible();
  await page.getByRole('button', { name: 'Inscribir estudiante' }).click();
  await page.getByLabel('Buscar estudiante').fill(studentCode);
  await page.getByRole('dialog').getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.getByRole('radio', { name: new RegExp(studentCode) }).check();
  await page
    .getByRole('dialog')
    .getByRole('combobox', { name: 'Grado', exact: true })
    .selectOption({ label: gradeName });
  await page
    .getByRole('dialog')
    .getByRole('combobox', { name: /^Sección/ })
    .selectOption({ label: 'A (0/30)' });
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('enroll-dialog.png'), fullPage: true });
  await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.locator('.sp-toast')).toContainText('La inscripción quedó registrada');
  const row = page.locator('tbody tr').filter({ hasText: studentCode });
  await expect(row).toContainText(gradeName);
  await expect(row).toContainText('A');

  await row.getByRole('button', { name: /Cambiar ubicación/ }).click();
  await page
    .getByRole('dialog')
    .getByRole('combobox', { name: /^Sección/ })
    .selectOption({ label: 'Sin sección' });
  await page.getByLabel('Vigente desde').fill(`${calendarYear}-02-10`);
  await page.getByLabel('Motivo del cambio').fill(reason);
  await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.locator('.sp-toast')).toContainText('la anterior se conserva');
  await expect(row).toContainText('Sin sección');
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('enrollment-roster.png'), fullPage: true });

  await page.goto('/workspace/people/' + studentId);
  const timeline = page.getByRole('list', { name: `Ubicaciones en el ciclo ${label}` });
  await expect(timeline.getByRole('listitem')).toHaveCount(2);
  await expect(timeline).toContainText('Sin sección');
  await expectAccessible(page);
});
