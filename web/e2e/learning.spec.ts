import { expect, test, type Browser, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

const reason = 'Autorización institucional sintética para prueba de navegador.';

async function login(page: Page, username: string, password: string): Promise<void> {
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

test('teacher publishes an activity, the student submits and receives feedback', async ({
  page,
  browser,
}) => {
  const s = randomUUID().slice(0, 5).toUpperCase();
  const password = 'Synthetic-Portal!12345';
  const title = 'Informe de lectura ' + s;
  await login(
    page,
    process.env['SCHOOLPLATFORM_E2E_USERNAME']!,
    process.env['SCHOOLPLATFORM_E2E_PASSWORD']!,
  );
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('academic.manage')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((x: { id: string }) => x.id === session.userId);
    await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
      version: me.version,
      reason,
      profile: 'administrator',
    });
    await login(
      page,
      process.env['SCHOOLPLATFORM_E2E_USERNAME']!,
      process.env['SCHOOLPLATFORM_E2E_PASSWORD']!,
    );
  }
  const origin = new URL(page.url()).origin;
  // Activities are delivered between publication and due date, so this test needs a year that spans today (shared by projects).
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
      code: 'A' + s,
      name: 'Grado tareas ' + s,
      stage: '',
      sequence: 80,
    })
  ).id;
  const subject = (
    await post(page, '/api/v1/academic/subjects', { code: 'R' + s, name: 'Lectura ' + s })
  ).id;
  await post(page, `/api/v1/years/${year}/courses`, {
    gradeLevelId: grade,
    sectionId: null,
    subjectIds: [subject],
  });
  const course = (
    await (await page.request.get(`/api/v1/years/${year}/courses?gradeId=${grade}`)).json()
  )[0].id;
  const student = (
    await post(page, '/api/v1/people', {
      givenNames: 'Iris',
      familyNames: 'Sintética ' + s,
      contacts: [],
      studentCode: 'TAR-' + s,
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
  const studentInvite = await post(page, '/api/v1/access/invitations', {
    username: 'stu-' + s.toLowerCase(),
    profile: 'basic',
    reason,
  });
  await post(page, `/api/v1/people/${student}/account`, {
    version: (await (await page.request.get(`/api/v1/people/${student}`)).json()).version,
    userId: studentInvite.userId,
    reason,
  });
  const teacher = (
    await post(page, '/api/v1/people', {
      givenNames: 'Hugo',
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
    username: 'doc-' + s.toLowerCase(),
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

  const teacherPage = await activate(
    browser,
    origin,
    teacherInvite.code,
    'doc-' + s.toLowerCase(),
    password,
  );
  await teacherPage.getByRole('link', { name: `Actividades de Lectura ${s}` }).click();
  await teacherPage.getByRole('button', { name: 'Nueva actividad' }).click();
  await teacherPage.getByRole('dialog').getByLabel('Título').fill(title);
  await teacherPage
    .getByRole('dialog')
    .getByLabel('Instrucciones')
    .fill('Resume el capítulo 3 en un párrafo.');
  const due = new Date(Date.now() + 3 * 864e5);
  await teacherPage
    .getByRole('dialog')
    .getByLabel('Fecha y hora de entrega')
    .fill(new Date(due.getTime() - due.getTimezoneOffset() * 60000).toISOString().slice(0, 16));
  await teacherPage.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();
  await teacherPage.getByRole('button', { name: `Publicar ${title}` }).click();
  await teacherPage
    .locator('.sp-alert')
    .getByRole('button', { name: /^Publicar/ })
    .click();
  await expect(teacherPage.locator('.sp-toast')).toContainText('se publicó');
  await expectAccessible(teacherPage);

  const studentPage = await activate(
    browser,
    origin,
    studentInvite.code,
    'stu-' + s.toLowerCase(),
    password,
  );
  await studentPage.getByRole('link', { name: 'Ver notas, tareas y asistencia' }).click();
  await expect(studentPage.getByRole('heading', { name: 'Tareas' })).toBeVisible();
  await studentPage.getByRole('button', { name: `Entregar ${title}` }).click();
  await studentPage
    .getByRole('dialog')
    .getByLabel('Tu entrega')
    .fill('El capítulo describe el viaje del protagonista.');
  await studentPage.getByRole('button', { name: 'Enviar entrega' }).click();
  await expect(studentPage.locator('.task-list')).toContainText('Entregada');
  // The dialog stays open after the first submission so the student can attach evidence.
  await studentPage
    .getByRole('dialog')
    .locator('input[type=file]')
    .setInputFiles({
      name: 'evidencia.png',
      mimeType: 'image/png',
      buffer: Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3, 4]),
    });
  await expect(
    studentPage.getByRole('dialog').getByRole('link', { name: 'evidencia.png' }),
  ).toBeVisible();
  await expectAccessible(studentPage);
  await studentPage.screenshot({
    path: test.info().outputPath('student-attachment.png'),
    fullPage: true,
  });
  await studentPage
    .getByRole('dialog')
    .getByRole('button', { name: 'Cerrar', exact: true })
    .click();
  await expectAccessible(studentPage);

  await teacherPage.getByRole('button', { name: `Ver entregas de ${title}` }).click();
  await expect(teacherPage.locator('.submission-list')).toContainText('viaje del protagonista');
  await expect(
    teacherPage.locator('.submission-list').getByRole('link', { name: /evidencia\.png/ }),
  ).toBeVisible();
  await teacherPage.getByRole('button', { name: `Retroalimentar a Iris Sintética ${s}` }).click();
  await teacherPage
    .getByRole('dialog')
    .getByLabel('Comentario para el estudiante')
    .fill('Buen resumen; agrega una cita.');
  await teacherPage.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(teacherPage.locator('.submission-list')).toContainText('Revisada');
  await teacherPage.screenshot({
    path: test.info().outputPath('teacher-activities.png'),
    fullPage: true,
  });

  await studentPage.reload();
  await expect(studentPage.locator('.task-list')).toContainText('Buen resumen');
  await expect(studentPage.locator('.task-list')).toContainText('Revisada');
  await studentPage.screenshot({
    path: test.info().outputPath('student-tasks.png'),
    fullPage: true,
  });

  // Attendance: the teacher marks a late arrival today and the student sees it in the summary.
  await teacherPage.goto(`/workspace/courses/${course}/attendance`);
  await teacherPage
    .getByRole('group', { name: `Asistencia de Iris Sintética ${s}` })
    .getByRole('radio', { name: 'Tarde' })
    .check();
  await teacherPage.getByLabel(`Observación de Iris Sintética ${s}`).fill('Llegó 10 minutos tarde');
  await teacherPage.getByRole('button', { name: 'Guardar asistencia' }).click();
  await expect(teacherPage.locator('.sp-toast')).toContainText('Asistencia guardada');
  await expectAccessible(teacherPage);
  await teacherPage.screenshot({ path: test.info().outputPath('attendance.png'), fullPage: true });
  await studentPage.reload();
  await expect(studentPage.locator('.attendance-summary')).toContainText('1 tardanza');
  await expect(studentPage.getByText('Llegó 10 minutos tarde')).toBeVisible();
  await expectAccessible(studentPage);
  await teacherPage.context().close();
  await studentPage.context().close();
});
