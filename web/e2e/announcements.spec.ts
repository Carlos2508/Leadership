import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

test.use({ trace: 'off' });

const reason = 'Autorización institucional sintética para prueba de navegador.';

async function login(page: Page, username: string, password: string): Promise<void> {
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Usuario', { exact: true }).fill(username);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(page.getByRole('heading', { name: /^¡Hola,/ })).toBeVisible();
}

async function post(page: Page, path: string, data: object) {
  const token = (await (await page.request.get('/api/v1/antiforgery')).json()).token as string;
  const response = await page.request.post(path, { data, headers: { 'X-XSRF-TOKEN': token } });
  expect(response.status(), path).toBeLessThan(300);
  return response.status() === 204 ? null : response.json();
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

test('coordination publishes a notice and the student reads it in the platform', async ({
  page,
  browser,
}, testInfo) => {
  const s = randomUUID().slice(0, 5).toUpperCase();
  const owner = process.env['SCHOOLPLATFORM_E2E_USERNAME']!;
  const ownerPassword = process.env['SCHOOLPLATFORM_E2E_PASSWORD']!;
  await login(page, owner, ownerPassword);
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('communication.publish')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((x: { id: string }) => x.id === session.userId);
    await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
      version: me.version,
      reason,
      profile: 'administrator',
    });
    await login(page, owner, ownerPassword);
  }
  const today = new Date();
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  const years = await (await page.request.get('/api/v1/academic/years')).json();
  let year = years.find(
    (y: { startsOn: string; endsOn: string }) => y.startsOn <= iso(today) && y.endsOn >= iso(today),
  )?.id;
  if (!year) {
    year = (
      await post(page, '/api/v1/academic/years', {
        label: 'Ciclo actual E2E',
        startsOn: iso(new Date(today.getTime() - 60 * 864e5)),
        endsOn: iso(new Date(today.getTime() + 200 * 864e5)),
      })
    ).id;
    await post(page, `/api/v1/academic/years/${year}/periods`, {
      name: null,
      startsOn: iso(new Date(today.getTime() - 60 * 864e5)),
      endsOn: iso(new Date(today.getTime() + 20 * 864e5)),
    });
    const detail = await (await page.request.get(`/api/v1/academic/years/${year}`)).json();
    await post(page, `/api/v1/academic/years/${year}/status`, {
      version: detail.year.version,
      status: 'EnrollmentOpen',
      reason,
    });
  }
  const grade = (
    await post(page, '/api/v1/academic/grades', {
      code: 'V' + s,
      name: 'Grado avisos ' + s,
      stage: '',
      sequence: 90,
    })
  ).id;
  const student = (
    await post(page, '/api/v1/people', {
      givenNames: 'Noa',
      familyNames: 'Sintética ' + s,
      contacts: [],
      studentCode: 'AVI-' + s,
      isGuardian: false,
      confirmDuplicate: true,
    })
  ).id;
  await post(page, '/api/v1/enrollments', {
    studentId: student,
    academicYearId: year,
    gradeLevelId: grade,
    sectionId: null,
    enrolledOn: null,
  });
  const invite = await post(page, '/api/v1/access/invitations', {
    username: 'avi-' + s.toLowerCase(),
    profile: 'basic',
    reason,
  });
  await post(page, `/api/v1/people/${student}/account`, {
    version: (await (await page.request.get(`/api/v1/people/${student}`)).json()).version,
    userId: invite.userId,
    reason,
  });

  const title = 'Día de convivencia ' + s;
  await page.goto('/workspace/announcements');
  await page.getByRole('button', { name: 'Nuevo aviso' }).click();
  await page.getByRole('dialog').getByLabel('Título').fill(title);
  await page
    .getByRole('dialog')
    .getByLabel('Mensaje')
    .fill('El viernes habrá actividades recreativas. Traer ropa cómoda.');
  await page
    .getByRole('dialog')
    .getByRole('checkbox', { name: /Encargados/ })
    .uncheck();
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('announcement-dialog.png'), fullPage: true });
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar borrador' }).click();
  await page.getByRole('button', { name: `Publicar ${title}` }).click();
  await page
    .locator('.sp-alert')
    .getByRole('button', { name: /^Publicar/ })
    .click();
  await expect(page.locator('.sp-toast')).toContainText('se publicó');
  await expectAccessible(page);

  const context = await browser.newContext({ baseURL: new URL(page.url()).origin });
  const studentPage = await context.newPage();
  await studentPage.goto('/Identity/Account/Activate');
  await studentPage.getByLabel('Código de acceso', { exact: true }).fill(invite.code);
  await studentPage.getByLabel('Nueva contraseña', { exact: true }).fill('Synthetic-Portal!12345');
  await studentPage
    .getByLabel('Confirmar contraseña', { exact: true })
    .fill('Synthetic-Portal!12345');
  await studentPage.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await login(studentPage, 'avi-' + s.toLowerCase(), 'Synthetic-Portal!12345');
  await expect(
    studentPage.getByRole('region', { name: 'Comunicados importantes', exact: true }),
  ).toContainText('sin leer');
  await studentPage
    .getByRole('region', { name: 'Comunicados importantes', exact: true })
    .getByRole('link', { name: 'Ver todos', exact: true })
    .click();
  await studentPage.getByRole('button', { name: new RegExp(title) }).click();
  await expect(studentPage.getByText('Traer ropa cómoda')).toBeVisible();
  await expectAccessible(studentPage);
  await studentPage.screenshot({
    path: testInfo.outputPath('student-announcements.png'),
    fullPage: true,
  });
  // Header bell: opens a panel; a notice already read on the page no longer appears there.
  await studentPage.getByRole('button', { name: /^Notificaciones/ }).click();
  const panel = studentPage.getByRole('dialog', { name: 'Notificaciones' });
  await expect(panel).toBeVisible();
  await expect(panel).not.toContainText(title);
  // Measure contrast once the opening animation has finished (mid-fade colours are not the final ones).
  await panel.evaluate((el) => Promise.all(el.getAnimations({ subtree: true }).map((a) => a.finished)));
  await expectAccessible(studentPage);
  await studentPage.keyboard.press('Escape');
  await expect(panel).toBeHidden();
  await context.close();

  await page.reload();
  await page.getByRole('tab', { name: 'Redactados' }).click();
  await expect(page.locator('.assignment-card').filter({ hasText: title })).toContainText(
    /[1-9]\d* lecturas?/,
  );
});
