import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

const reason = 'Autorización institucional sintética para prueba de navegador.';

async function login(page: Page, username: string, password: string): Promise<void> {
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Usuario', { exact: true }).fill(username);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(page.getByRole('heading', { name: /bienvenida/i })).toBeVisible();
}

async function post(page: Page, path: string, data: object) {
  const token = (await (await page.request.get('/api/v1/antiforgery')).json()).token as string;
  const response = await page.request.post(path, { data, headers: { 'X-XSRF-TOKEN': token } });
  expect(response.status(), path).toBeLessThan(300);
  return response.status() === 204 ? null : response.json();
}

async function expectAccessible(page: Page): Promise<void> {
  expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze()).violations).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
}

test('organize courses by section, assign a teacher and show the teacher only their course roster', async ({ page, browser }, testInfo) => {
  const owner = process.env['SCHOOLPLATFORM_E2E_USERNAME']!;
  const ownerPassword = process.env['SCHOOLPLATFORM_E2E_PASSWORD']!;
  const suffix = randomUUID().slice(0, 5).toUpperCase();
  const calendarYear = 2080 + testInfo.repeatEachIndex * 2 + (testInfo.project.name === 'mobile' ? 1 : 0);
  const subject = 'Matemática ' + suffix;
  const teacherUser = 'teacher-' + suffix.toLowerCase();
  const teacherPassword = 'Teacher-Synthetic!12345';

  await login(page, owner, ownerPassword);
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('academic.manage')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((x: { id: string }) => x.id === session.userId);
    await post(page, `/api/v1/access/accounts/${me.id}/profile`, { version: me.version, reason, profile: 'administrator' });
    await login(page, owner, ownerPassword);
  }
  const year = (await post(page, '/api/v1/academic/years', { label: `${calendarYear}-${suffix}`, startsOn: `${calendarYear}-01-10`, endsOn: `${calendarYear}-10-30` })).id;
  await post(page, `/api/v1/academic/years/${year}/periods`, { name: null, startsOn: `${calendarYear}-01-10`, endsOn: `${calendarYear}-03-10` });
  const detail = await (await page.request.get(`/api/v1/academic/years/${year}`)).json();
  await post(page, `/api/v1/academic/years/${year}/status`, { version: detail.year.version, status: 'EnrollmentOpen', reason });
  const gradeName = 'Grado cursos ' + suffix;
  const grade = (await post(page, '/api/v1/academic/grades', { code: 'K' + suffix, name: gradeName, stage: '', sequence: 60 })).id;
  const section = (await post(page, `/api/v1/academic/years/${year}/sections`, { gradeLevelId: grade, name: 'A', shift: null, capacity: null })).id;
  const student = (await post(page, '/api/v1/people', { givenNames: 'Tomás', familyNames: 'Sintético ' + suffix, contacts: [], studentCode: 'CUR-' + suffix, isGuardian: false, confirmDuplicate: true })).id;
  await post(page, '/api/v1/enrollments', { studentId: student, academicYearId: year, gradeLevelId: grade, sectionId: section, enrolledOn: `${calendarYear}-01-10` });
  const teacher = (await post(page, '/api/v1/people', { givenNames: 'Marta', familyNames: 'Docente ' + suffix, contacts: [], studentCode: null, isGuardian: false, confirmDuplicate: true })).id;
  const teacherDetail = await (await page.request.get(`/api/v1/people/${teacher}`)).json();
  await post(page, `/api/v1/people/${teacher}/roles`, { version: teacherDetail.version, role: 'staff', position: 'Docente' });
  const invite = await post(page, '/api/v1/access/invitations', { username: teacherUser, profile: 'basic', reason });
  const linked = await (await page.request.get(`/api/v1/people/${teacher}`)).json();
  await post(page, `/api/v1/people/${teacher}/account`, { version: linked.version, userId: invite.userId, reason });

  await page.goto(`/workspace/courses?year=${year}`);
  await expect(page.getByRole('heading', { name: 'Cursos y docentes', level: 1 })).toBeVisible();
  await page.getByRole('button', { name: '+ Nueva' }).click();
  await page.getByRole('dialog').getByLabel('Código').fill('M' + suffix);
  await page.getByRole('dialog').getByLabel('Nombre').fill(subject);
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.locator('p.success-feedback')).toContainText('La materia se guardó');

  await page.getByRole('button', { name: 'Crear cursos' }).click();
  await page.getByRole('dialog').getByRole('combobox', { name: 'Grado' }).selectOption({ label: gradeName });
  await page.getByRole('dialog').getByRole('combobox', { name: 'Alcance' }).selectOption({ label: 'Sección A' });
  await page.getByRole('dialog').getByRole('checkbox', { name: subject }).check();
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.locator('p.success-feedback')).toContainText('Se crearon 1 cursos');

  await page.getByRole('button', { name: `+ Docente para ${subject}` }).click();
  await page.getByRole('dialog').getByLabel('Buscar personal').fill('Docente ' + suffix);
  await page.getByRole('dialog').getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.getByRole('radio', { name: new RegExp('Marta Docente ' + suffix) }).check();
  await page.getByRole('dialog').getByLabel('Motivo').fill(reason);
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.locator('.course-item').filter({ hasText: subject })).toContainText('Marta Docente ' + suffix);
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('courses.png'), fullPage: true });

  const teacherContext = await browser.newContext({ baseURL: new URL(page.url()).origin });
  const teacherPage = await teacherContext.newPage();
  await teacherPage.goto('/Identity/Account/Activate');
  await teacherPage.getByLabel('Código de acceso', { exact: true }).fill(invite.code);
  await teacherPage.getByLabel('Nueva contraseña', { exact: true }).fill(teacherPassword);
  await teacherPage.getByLabel('Confirmar contraseña', { exact: true }).fill(teacherPassword);
  await teacherPage.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await login(teacherPage, teacherUser, teacherPassword);
  await expect(teacherPage.getByRole('heading', { name: 'Mis cursos' })).toBeVisible();
  await teacherPage.getByRole('link', { name: `Ver lista de ${subject}` }).click();
  await expect(teacherPage.getByRole('heading', { name: subject, level: 1 })).toBeVisible();
  await expect(teacherPage.locator('tbody')).toContainText('Tomás Sintético ' + suffix);
  await expectAccessible(teacherPage);
  await teacherPage.screenshot({ path: testInfo.outputPath('teacher-roster.png'), fullPage: true });
  expect((await teacherPage.request.get(`/api/v1/years/${year}/courses`)).status()).toBe(403);
  await teacherContext.close();
});
