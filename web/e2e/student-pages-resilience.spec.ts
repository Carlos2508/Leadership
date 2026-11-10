import { expect, test, type Browser, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { courseFixture, login, post, reason } from './support/school';

async function fixture(page: Page, browser: Browser, mobile: boolean, self = false) {
  const f = await courseFixture(page, mobile ? 2063 : 2062);
  const person = self
    ? f.students[0]
    : (
        await post(page, '/api/v1/people', {
          givenNames: 'Encargada',
          familyNames: 'Revisión ' + f.suffix,
          contacts: [],
          isGuardian: true,
          confirmDuplicate: true,
        })
      ).id;
  if (!self)
    for (const studentId of f.students)
      await post(page, '/api/v1/relationships', {
        studentId,
        guardianId: person,
        kind: 'Mother',
        faculties: ['AcademicRecords'],
        reason,
      });
  const username = (self ? 'student-review-' : 'family-review-') + f.suffix;
  const password = 'Synthetic-Only!' + f.suffix + '12';
  const invitation = await post(page, '/api/v1/access/invitations', {
    username,
    profile: 'basic',
    reason,
  });
  const detail = await (await page.request.get('/api/v1/people/' + person)).json();
  await post(page, `/api/v1/people/${person}/account`, {
    version: detail.version,
    userId: invitation.userId,
    reason,
  });
  const context = await browser.newContext({
    baseURL: new URL(page.url()).origin,
    viewport: mobile ? { width: 390, height: 844 } : { width: 1440, height: 1000 },
    isMobile: mobile,
    hasTouch: mobile,
  });
  const family = await context.newPage();
  await family.goto('/Identity/Account/Activate');
  await family.getByLabel('Código de acceso', { exact: true }).fill(invitation.code);
  await family.getByLabel('Nueva contraseña', { exact: true }).fill(password);
  await family.getByLabel('Confirmar contraseña', { exact: true }).fill(password);
  await family.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await family.goto('/Identity/Account/Login');
  await family.getByLabel('Usuario', { exact: true }).fill(username);
  await family.getByLabel('Contraseña', { exact: true }).fill(password);
  await family.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await family.waitForURL(/workspace/);
  const views = await Promise.all(
    f.students
      .slice(0, self ? 1 : 2)
      .map(async (id) => (await family.request.get('/api/v1/me/students/' + id)).json()),
  );
  return { ...f, context, family, views };
}
async function history(page: Page, url: string) {
  await page.evaluate((url) => {
    window.history.pushState({}, '', url);
    window.dispatchEvent(new PopStateEvent('popstate'));
  }, url);
}
function task(id: string, title: string) {
  return {
    id,
    course: 'Materia de ' + title,
    title,
    instructions: 'Trabajo sintético para revisión visual y recuperación de errores.',
    dueAt: '2062-02-12T18:00:00Z',
    allowLate: true,
    closed: false,
    submissionStatus: 'Submitted',
    submittedAt: '2026-11-09T18:00:00Z',
    late: false,
    text: 'Entrega original',
    feedback: null,
    version: 'AAAAAAAACAA=',
    canSubmit: true,
    points: 20,
    period: 'Primer período',
    publishedAt: '2026-11-08T18:00:00Z',
  };
}
async function accessible(page: Page) {
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
}

for (const section of ['summary', 'tasks', 'grades', 'attendance'] as const) {
  test(`${section} names the student and never shows a sibling's data while loading or after a late response`, async ({
    page,
    browser,
  }, info) => {
    test.setTimeout(60_000);
    const f = await fixture(page, browser, info.project.name === 'mobile');
    const p = f.family;
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => (release = resolve));
    let delay = false;
    let requested: () => void = () => {};
    const arrived = new Promise<void>((resolve) => (requested = resolve));
    const endpoint =
      section === 'tasks'
        ? 'assignments'
        : section === 'attendance'
          ? 'attendance/calendar'
          : 'grades';
    const suffix = section === 'summary' ? '' : '/' + section;
    const studentName = p.locator(section === 'summary' ? 'main h1' : '.student-hero-name');
    const today = await p.evaluate(() => {
      const d = new Date();
      return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    });
    await p.route('**/api/v1/me/students/*/' + endpoint + '*', async (route) => {
      const second = route.request().url().includes(f.students[1]);
      const child = second ? 'Bruno' : 'Ana';
      if (delay && second) {
        requested();
        await held;
      }
      const json =
        section === 'tasks'
          ? [task('a0000000-0000-0000-0000-000000000001', 'Exclusiva de ' + child)]
          : section === 'grades' || section === 'summary'
            ? {
                studentId: second ? f.students[1] : f.students[0],
                year: '2062',
                periods: [
                  {
                    periodId: 'p1',
                    sequence: 1,
                    name: 'Primer período',
                    status: 'Available',
                    grades: [
                      {
                        subject: 'Exclusiva de ' + child,
                        score: second ? 65 : 90,
                        status: 'Scored',
                        activities: [
                          {
                            title: 'Proyecto de revisión',
                            maxPoints: 100,
                            points: second ? 65 : 90,
                            status: 'Scored',
                            assignmentId: null,
                          },
                        ],
                      },
                    ],
                    pending: [],
                  },
                ],
              }
            : {
                from: today,
                to: today,
                present: second ? 3 : 9,
                absent: 0,
                late: 0,
                excused: 0,
                days: [
                  {
                    date: today,
                    entries: [{ subject: 'Exclusiva de ' + child, status: 'Present', note: '' }],
                  },
                ],
              };
      await route.fulfill({ json });
    });
    await p.goto(`/workspace/my/students/${f.students[0]}${suffix}`);
    await expect(studentName).toHaveText(f.views[0].student.displayName);
    await expect(p.getByText('Exclusiva de Ana', { exact: true }).first()).toBeVisible();
    delay = true;
    await history(p, `/workspace/my/students/${f.students[1]}${suffix}`);
    await arrived;
    await expect(p.getByText('Exclusiva de Ana', { exact: true })).toHaveCount(0);
    await expect(studentName).toHaveText(f.views[1].student.displayName);
    await p.screenshot({ path: info.outputPath(`${section}-loading-sibling.png`), fullPage: true });
    await history(p, `/workspace/my/students/${f.students[0]}${suffix}`);
    await expect(p.getByText('Exclusiva de Ana', { exact: true }).first()).toBeVisible();
    release();
    await p.waitForTimeout(200);
    await expect(studentName).toHaveText(f.views[0].student.displayName);
    await expect(p.getByText('Exclusiva de Bruno', { exact: true })).toHaveCount(0);
    await accessible(p);
    await p.screenshot({ path: info.outputPath(`${section}-correct-student.png`), fullPage: true });
    await f.context.close();
  });
}

test('submission files show a retryable error, discard stale results and protect unsaved text', async ({
  page,
  browser,
}, info) => {
  test.setTimeout(60_000);
  const f = await fixture(page, browser, info.project.name === 'mobile', true);
  const p = f.family;
  const first = 'a0000000-0000-0000-0000-000000000001',
    second = 'a0000000-0000-0000-0000-000000000002';
  let fail = true;
  let delay = false;
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => (release = resolve));
  await p.route(`**/api/v1/me/students/${f.students[0]}/assignments`, (route) =>
    route.fulfill({ json: [task(first, 'Entrega A'), task(second, 'Entrega B')] }),
  );
  await p.route('**/api/v1/me/assignments/*/files', async (route) => {
    const a = route.request().url().includes(first);
    if (a && fail) {
      await route.fulfill({
        status: 503,
        json: { title: 'No pudimos consultar los archivos de prueba.' },
      });
      return;
    }
    if (a && delay) await held;
    await route.fulfill({
      json: [
        { id: a ? 'file-a' : 'file-b', fileName: a ? 'archivo-a.txt' : 'archivo-b.txt', size: 25 },
      ],
    });
  });
  await p.goto(`/workspace/my/students/${f.students[0]}/tasks`);
  await p.getByRole('button', { name: 'Actualizar entrega Entrega A', exact: true }).click();
  const dialog = p.locator('dialog[open]');
  await expect(dialog.getByRole('alert')).toContainText('No pudimos consultar');
  await expect(dialog).not.toContainText('Sin archivos.');
  fail = false;
  await dialog.getByRole('button', { name: 'Reintentar archivos', exact: true }).click();
  await expect(dialog.getByRole('link', { name: 'archivo-a.txt' })).toBeVisible();
  await dialog
    .getByRole('textbox', { name: 'Tu entrega', exact: true })
    .fill('Borrador que debe conservarse al cancelar la salida.');
  await dialog.getByRole('button', { name: 'Cerrar', exact: true }).click();
  await p.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await expect(dialog.getByRole('textbox', { name: 'Tu entrega', exact: true })).toHaveValue(
    'Borrador que debe conservarse al cancelar la salida.',
  );
  await history(p, '/workspace');
  await p.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await expect(p).toHaveURL(new RegExp(`/students/${f.students[0]}/tasks`));
  await p.screenshot({ path: info.outputPath('submission-preserved-draft.png'), fullPage: true });
  await dialog.getByRole('button', { name: 'Cerrar', exact: true }).click();
  await p.getByRole('button', { name: 'Descartar texto', exact: true }).click();
  delay = true;
  await p.getByRole('button', { name: 'Actualizar entrega Entrega A', exact: true }).click();
  await dialog.getByRole('button', { name: 'Cerrar', exact: true }).click();
  await p.getByRole('button', { name: 'Actualizar entrega Entrega B', exact: true }).click();
  await expect(dialog.getByRole('link', { name: 'archivo-b.txt' })).toBeVisible();
  release();
  await p.waitForTimeout(200);
  await expect(dialog.getByRole('link', { name: 'archivo-a.txt' })).toHaveCount(0);
  await accessible(p);
  await p.screenshot({ path: info.outputPath('submission-correct-files.png'), fullPage: true });
  await f.context.close();
});

test('student summary and gate failures remain retryable without claiming empty records', async ({
  page,
  browser,
}, info) => {
  test.setTimeout(60_000);
  const f = await fixture(page, browser, info.project.name === 'mobile', true);
  const p = f.family;
  const failed = '**/api/v1/me/students/*/{assignments,grades,gate,attendance}';
  await p.route(failed, (route) =>
    route.fulfill({ status: 503, json: { title: 'Consulta temporalmente no disponible' } }),
  );
  await p.goto(`/workspace/my/students/${f.students[0]}`);
  await expect(p.getByRole('alert')).toContainText('algunos datos del resumen');
  await expect(p.getByText('No se pudieron consultar las tareas.', { exact: true })).toBeVisible();
  await expect(
    p.getByText('No se pudieron consultar las calificaciones.', { exact: true }),
  ).toBeVisible();
  await expect(p.getByText('No se pudo consultar la asistencia.', { exact: true })).toBeVisible();
  await expect(
    p.getByText('No se pudieron consultar las entradas y salidas.', { exact: true }),
  ).toBeVisible();
  await expect(p.getByText('No hay entregas pendientes.', { exact: true })).toHaveCount(0);
  await expect(p.getByText('Sin marcas en los últimos 45 días.', { exact: true })).toHaveCount(0);
  await p.screenshot({ path: info.outputPath('summary-retryable-errors.png'), fullPage: true });
  await p.unroute(failed);
  await p.getByRole('button', { name: 'Reintentar resumen', exact: true }).click();
  await expect(p.getByText('No hay entregas pendientes.', { exact: true })).toBeVisible();
  await expect(p.getByRole('alert')).toHaveCount(0);
  await p.route('**/api/v1/me/students/*/gate', (route) =>
    route.fulfill({ status: 503, json: { title: 'Portería temporalmente no disponible' } }),
  );
  await p.goto(`/workspace/my/students/${f.students[0]}/attendance`);
  await expect(p.getByRole('alert')).toContainText('marcas de portería');
  await p.unroute('**/api/v1/me/students/*/gate');
  await p.getByRole('button', { name: 'Reintentar portería', exact: true }).click();
  await expect(p.getByRole('alert')).toHaveCount(0);
  await accessible(p);
  await f.context.close();
});

test('warehouse and timetable forms keep actions visible on short screens', async ({
  page,
}, info) => {
  await page.setViewportSize({ width: info.project.name === 'mobile' ? 320 : 1024, height: 640 });
  const f = await courseFixture(page, info.project.name === 'mobile' ? 2065 : 2064);
  for (const [name, url, trigger] of [
    ['warehouse', '/workspace/inventory', 'Nuevo artículo'],
    [
      'timetable',
      `/workspace/timetables/edit?year=${f.yearId}&grade=${f.grade}`,
      'Configurar estructura',
    ],
  ]) {
    await page.goto(url);
    await page.getByRole('button', { name: trigger, exact: true }).first().click();
    const dialog = page.locator('dialog[open]');
    await expect(dialog).toBeVisible();
    const actions = dialog.locator('.dialog-actions button');
    for (const action of await actions.all()) {
      await expect(action).toBeInViewport({ ratio: 1 });
    }
    await accessible(page);
    await page.screenshot({
      path: info.outputPath(`${name}-visible-actions.png`),
      fullPage: false,
    });
    await page.keyboard.press('Escape');
  }
});

test('settings can recover from an initial loading failure without retaining the error', async ({
  page,
}) => {
  await login(page);
  await page.route(
    '**/api/v1/settings',
    (route) =>
      route.fulfill({ status: 503, json: { title: 'Ajustes temporalmente no disponibles' } }),
    { times: 1 },
  );
  await page.goto('/workspace/settings');
  await expect(page.getByRole('alert')).toContainText('temporalmente');
  await page.getByRole('button', { name: 'Reintentar', exact: true }).click();
  await expect(page.getByRole('spinbutton', { name: /^Tamaño máximo por archivo/ })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});
