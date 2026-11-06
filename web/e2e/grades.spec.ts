import { expect, test, type Browser, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';
import { schoolToday } from './school-date';

const reason = 'Autorización institucional sintética para prueba de navegador.';

async function login(page: Page, username: string, password: string): Promise<void> {
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Usuario', { exact: true }).fill(username);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(
    page.getByRole('heading', { name: /^¡Hola,/ }),
  ).toBeVisible();
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

async function activate(
  browser: Browser,
  origin: string,
  code: string,
  username: string,
  password: string,
): Promise<Page> {
  const context = await browser.newContext({ baseURL: origin });
  const page = await context.newPage();
  await page.goto('/Identity/Account/Activate');
  await page.getByLabel('Código de acceso', { exact: true }).fill(code);
  await page.getByLabel('Nueva contraseña', { exact: true }).fill(password);
  await page.getByLabel('Confirmar contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await login(page, username, password);
  return page;
}

test('teacher captures, coordination publishes and the family sees grades only once tuition is covered', async ({
  page,
  browser,
}, testInfo) => {
  const owner = process.env['SCHOOLPLATFORM_E2E_USERNAME']!;
  const ownerPassword = process.env['SCHOOLPLATFORM_E2E_PASSWORD']!;
  const s = randomUUID().slice(0, 5).toUpperCase();
  const calendarYear =
    2090 + testInfo.repeatEachIndex * 2 + (testInfo.project.name === 'mobile' ? 1 : 0);
  const password = 'Synthetic-Portal!12345';
  const subject = 'Lenguaje ' + s;

  await login(page, owner, ownerPassword);
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('finance.charges')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((x: { id: string }) => x.id === session.userId);
    await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
      version: me.version,
      reason,
      profile: 'administrator',
    });
    await login(page, owner, ownerPassword);
  }
  const origin = new URL(page.url()).origin;
  const year = (
    await post(page, '/api/v1/academic/years', {
      label: `${calendarYear}-${s}`,
      startsOn: `${calendarYear}-01-10`,
      endsOn: `${calendarYear}-10-30`,
    })
  ).id;
  await post(page, `/api/v1/academic/years/${year}/periods`, {
    name: null,
    startsOn: `${calendarYear}-01-10`,
    endsOn: `${calendarYear}-03-10`,
  });
  const yearDetail = await (await page.request.get(`/api/v1/academic/years/${year}`)).json();
  await post(page, `/api/v1/academic/years/${year}/status`, {
    version: yearDetail.year.version,
    status: 'EnrollmentOpen',
    reason,
  });
  const grade = (
    await post(page, '/api/v1/academic/grades', {
      code: 'N' + s,
      name: 'Grado notas ' + s,
      stage: '',
      sequence: 70,
    })
  ).id;
  const subjectId = (
    await post(page, '/api/v1/academic/subjects', { code: 'L' + s, name: subject })
  ).id;
  await post(page, `/api/v1/years/${year}/courses`, {
    gradeLevelId: grade,
    sectionId: null,
    subjectIds: [subjectId],
  });
  const course = (
    await (await page.request.get(`/api/v1/years/${year}/courses?gradeId=${grade}`)).json()
  )[0].id;
  const student = (
    await post(page, '/api/v1/people', {
      givenNames: 'Ema',
      familyNames: 'Sintética ' + s,
      contacts: [],
      studentCode: 'NOT-' + s,
      isGuardian: false,
      confirmDuplicate: true,
    })
  ).id;
  await post(page, '/api/v1/enrollments', {
    studentId: student,
    academicYearId: year,
    gradeLevelId: grade,
    sectionId: null,
    enrolledOn: `${calendarYear}-01-10`,
  });
  const classmate = (
    await post(page, '/api/v1/people', {
      givenNames: 'Iván',
      familyNames: 'Zeta ' + s,
      contacts: [],
      studentCode: 'NOU-' + s,
      isGuardian: false,
      confirmDuplicate: true,
    })
  ).id;
  await post(page, '/api/v1/enrollments', {
    studentId: classmate,
    academicYearId: year,
    gradeLevelId: grade,
    sectionId: null,
    enrolledOn: `${calendarYear}-01-10`,
  });

  const teacher = (
    await post(page, '/api/v1/people', {
      givenNames: 'Raúl',
      familyNames: 'Docente ' + s,
      contacts: [],
      studentCode: null,
      isGuardian: false,
      confirmDuplicate: true,
    })
  ).id;
  await post(page, `/api/v1/people/${teacher}/roles`, {
    version: (await (await page.request.get(`/api/v1/people/${teacher}`)).json()).version,
    role: 'staff',
    position: 'Docente',
  });
  const teacherInvite = await post(page, '/api/v1/access/invitations', {
    username: 'tch-' + s.toLowerCase(),
    profile: 'basic',
    reason,
  });
  await post(page, `/api/v1/people/${teacher}/account`, {
    version: (await (await page.request.get(`/api/v1/people/${teacher}`)).json()).version,
    userId: teacherInvite.userId,
    reason,
  });
  await post(page, `/api/v1/courses/${course}/teachers`, {
    staffId: teacher,
    role: 'Lead',
    startsOn: null,
    reason,
  });

  const guardian = (
    await post(page, '/api/v1/people', {
      givenNames: 'Olga',
      familyNames: 'Sintética ' + s,
      contacts: [],
      studentCode: null,
      isGuardian: true,
      confirmDuplicate: true,
    })
  ).id;
  await post(page, '/api/v1/relationships', {
    studentId: student,
    guardianId: guardian,
    kind: 'Mother',
    startsOn: null,
    faculties: ['AcademicRecords', 'Finance'],
    reason,
  });
  const guardianInvite = await post(page, '/api/v1/access/invitations', {
    username: 'fam-' + s.toLowerCase(),
    profile: 'basic',
    reason,
  });
  await post(page, `/api/v1/people/${guardian}/account`, {
    version: (await (await page.request.get(`/api/v1/people/${guardian}`)).json()).version,
    userId: guardianInvite.userId,
    reason,
  });

  const concept = (
    await post(page, '/api/v1/finance/concepts', {
      code: 'T' + s,
      name: 'Colegiatura notas ' + s,
      isRecurring: true,
      countsForClearance: true,
    })
  ).id;
  const billing = (
    await post(page, `/api/v1/finance/years/${year}/periods`, {
      label: 'Cuota 1',
      dueOn: `${calendarYear}-02-05`,
    })
  ).id;
  await post(page, `/api/v1/finance/years/${year}/cutoffs`, {
    name: 'Corte primer período',
    throughSequence: 1,
  });
  const generation = {
    academicYearId: year,
    conceptId: concept,
    billingPeriodId: billing,
    occurrence: 1,
    amount: 300,
    dueOn: null,
    description: null,
    gradeIds: [grade],
    sectionIds: [],
    excludedEnrollmentIds: [],
  };
  const preview = await post(page, '/api/v1/finance/charges/preview', generation);
  await post(page, '/api/v1/finance/charges/generate', {
    ...generation,
    previewHash: preview.previewHash,
  });

  // Teacher captures the period score
  const teacherPage = await activate(
    browser,
    origin,
    teacherInvite.code,
    'tch-' + s.toLowerCase(),
    password,
  );
  await teacherPage.getByRole('link', { name: `Libreta de ${subject}` }).click();
  await expect(teacherPage.getByRole('heading', { name: subject, level: 1 })).toBeVisible();
  // Spreadsheet-like capture: paste a column from Excel, autosave, Enter moves to the next student.
  const ema = teacherPage.getByRole('textbox', { name: 'Nota de Ema Sintética ' + s });
  const ivan = teacherPage.getByRole('textbox', { name: 'Nota de Iván Zeta ' + s });
  await ema.focus();
  await ema.evaluate((input) => {
    const data = new DataTransfer();
    data.setData('text/plain', '91,5\r\n80\r\n70\r\n');
    input.dispatchEvent(
      new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }),
    );
  });
  await expect(ema).toHaveValue('91,5');
  await expect(ivan).toHaveValue('80');
  await expect(teacherPage.locator('.save-state')).toContainText('Guardado a las');
  await expect(teacherPage.locator('.save-state')).toContainText('2 notas');
  await expect(teacherPage.locator('.directory-summary')).toContainText('2 de 2 registradas');
  await ema.focus();
  await ema.press('Enter');
  await expect(ivan).toBeFocused();
  await ivan.pressSequentially('1000');
  await expect(teacherPage.locator('.field-error')).toContainText('Entre 0 y 100');
  await ivan.fill('88');
  await expect(teacherPage.locator('.field-error')).toHaveCount(0);
  await expect(teacherPage.locator('.save-state')).toContainText('1 nota');
  await teacherPage.reload();
  await expect(ivan).toHaveValue('88');
  await expect(ema).toHaveValue('91.5');
  await expectAccessible(teacherPage);
  await teacherPage.screenshot({ path: testInfo.outputPath('gradebook.png'), fullPage: true });
  await teacherPage.context().close();

  // Coordination publishes with the cutoff
  await page.goto(`/workspace/grades?year=${year}`);
  await expect(page.locator('.publication-list')).toContainText('2 de 2 notas');
  await page.getByRole('button', { name: /Publicar Bimestre 1/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Publicar' }).click();
  await expect(page.locator('.sp-toast')).toContainText('quedó publicado');
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('publications.png'), fullPage: true });

  // Family: blocked while the tuition of the cutoff is pending
  const familyPage = await activate(
    browser,
    origin,
    guardianInvite.code,
    'fam-' + s.toLowerCase(),
    password,
  );
  await familyPage.locator('#content').getByRole('link', { name: 'Ema Sintética ' + s }).click();
  await expect(familyPage.locator('.blocked-grades')).toContainText('colegiaturas requeridas');
  await expect(familyPage.locator('.blocked-grades')).toContainText('Cuota 1');
  await expect(familyPage.getByText('91.5')).toHaveCount(0);
  await expectAccessible(familyPage);
  await familyPage.screenshot({ path: testInfo.outputPath('family-blocked.png'), fullPage: true });

  const statement = await (
    await page.request.get(`/api/v1/finance/students/${student}/statement`)
  ).json();
  await post(page, '/api/v1/finance/payments', {
    idempotencyKey: randomUUID().replaceAll('-', ''),
    payerName: 'Olga Sintética',
    method: 'Cash',
    reference: null,
    receivedOn: await schoolToday(page),
    note: null,
    amount: 300,
    allocations: [{ chargeId: statement.charges[0].id, amount: 300 }],
  });
  await familyPage.reload();
  await expect(familyPage.locator('.grade-value')).toContainText('91.5');
  await expectAccessible(familyPage);
  await familyPage.screenshot({ path: testInfo.outputPath('family-grades.png'), fullPage: true });
  await familyPage.context().close();
});
