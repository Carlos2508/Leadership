import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomBytes, randomUUID } from 'node:crypto';
import { login, post, reason } from './support/school';
import { schoolToday } from './school-date';

// Activation codes/passwords belong only to the owned E2E database and are never retained in traces.
test.use({ trace: 'off' });

test('optional public content and unknown contact details stay hidden', async ({ page }) => {
  await page.route('**/api/v1/bootstrap', async (route) => {
    const response = await route.fetch();
    const body = await response.json();
    body.displayName = 'Comunidad de ejemplo';
    body.shortName = 'Mi colegio';
    body.campusLabel = '';
    body.public = Object.fromEntries(
      Object.entries(body.public).map(([key, value]) => [key, Array.isArray(value) ? [] : '']),
    );
    body.contact = {
      phone: '',
      email: '',
      address: '',
      website: '',
      dialPrefix: '+502',
      phones: [],
      whatsApp: '',
      officeHours: [],
      mapQuery: '',
      social: { instagram: '', facebook: '', youTube: '', tikTok: '' },
    };
    body.assets.heroImage = body.assets.studentImage = body.assets.communityImage = '';
    await route.fulfill({ response, json: body });
  });
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Comunidad de ejemplo');
  await expect(
    page.locator('#nosotros, #vida-escolar, #admision, #comunidad, #contacto'),
  ).toHaveCount(0);
  await expect(page.locator('iframe, .site-location')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Solicitar información' })).toHaveCount(0);
  expect(await page.locator('a[href^="tel:"], a[href^="mailto:"], a[href*="wa.me"]').count()).toBe(
    0,
  );
  expect(
    (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
      .violations,
  ).toEqual([]);
});

test('public reference sections have real destinations and a usable mobile menu', async ({
  page,
}, testInfo) => {
  await page.goto('/');
  const nav = page.getByRole('navigation', { name: 'Navegación pública' });
  for (const [name, id] of [
    ['Nosotros', 'nosotros'],
    ['Vida Escolar', 'vida-escolar'],
    ['Admisión', 'admision'],
    ['Comunidad', 'comunidad'],
    ['Contacto', 'contacto'],
  ]) {
    if (testInfo.project.name === 'mobile')
      await page.getByRole('button', { name: 'Menú de navegación' }).click();
    await nav.getByRole('link', { name, exact: true }).click();
    await expect(page).toHaveURL(new RegExp('#' + id + '$'));
    await expect
      .poll(() =>
        page.locator('#' + id).evaluate((el) => {
          const top = el.getBoundingClientRect().top;
          return top >= -1 && top < innerHeight * 0.7
            ? 'visible'
            : el.id + ': top=' + top + ', viewport=' + innerHeight;
        }),
      )
      .toBe('visible');
    if (testInfo.project.name === 'mobile')
      await expect(page.getByRole('button', { name: 'Menú de navegación' })).toHaveAttribute(
        'aria-expanded',
        'false',
      );
  }
  await page.getByText('¿Cómo solicito información de admisión?', { exact: true }).click();
  await expect(page.locator('details[open]')).toContainText('Comunícate por nuestros canales');
  expect(await page.locator('iframe').count()).toBe(0);
  await expect(
    page.locator('#contacto').getByRole('link', { name: /Cómo llegar/ }),
  ).toHaveAttribute('href', /^https:\/\/www.google.com\/maps\/dir\//);
  await expect(page.locator('#contacto').getByRole('link', { name: /WhatsApp/ })).toHaveAttribute(
    'href',
    /^https:\/\/wa.me\/502/,
  );
  await page.goto('/');
  for (const width of [320, 390, 900, 1440, 1920]) {
    await page.setViewportSize({ width, height: 1000 });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
      `public overflow ${width}`,
    ).toBeTruthy();
    expect(
      (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
        .violations,
    ).toEqual([]);
    await page.screenshot({
      path: testInfo.outputPath(`reference-public-${width}.png`),
      fullPage: true,
    });
    if (width === 900)
      await page.screenshot({ path: testInfo.outputPath('public-reference-viewport.png') });
  }
});

test('student reference dashboard uses authorized SQL data and its calendar and shortcuts work', async ({
  page,
}, testInfo) => {
  test.setTimeout(90_000);
  await login(page);
  const suffix = randomUUID().slice(0, 7);
  const today = await schoolToday(page);
  const now = new Date();
  const iso = (days: number) => new Date(now.getTime() + days * 864e5).toISOString().slice(0, 10);
  const years = await (await page.request.get('/api/v1/academic/years')).json();
  let yearId = years.find(
    (y: { startsOn: string; endsOn: string }) => y.startsOn <= today && y.endsOn >= today,
  )?.id;
  if (!yearId) {
    yearId = (
      await post(page, '/api/v1/academic/years', {
        label: 'Ciclo actual E2E',
        startsOn: iso(-60),
        endsOn: iso(200),
      })
    ).id;
    await post(page, `/api/v1/academic/years/${yearId}/periods`, {
      startsOn: iso(-60),
      endsOn: iso(20),
    });
    const detail = await (await page.request.get(`/api/v1/academic/years/${yearId}`)).json();
    await post(page, `/api/v1/academic/years/${yearId}/status`, {
      version: detail.year.version,
      status: 'EnrollmentOpen',
      reason,
    });
  }
  const grade = (
    await post(page, '/api/v1/academic/grades', {
      code: 'REF' + suffix,
      name: 'Básico · prueba visual ' + suffix,
      stage: '',
      sequence: 91,
    })
  ).id;
  const subjects: string[] = [];
  const availableSubjects = await (await page.request.get('/api/v1/academic/subjects')).json();
  for (const name of ['Ciencias Naturales', 'Matemáticas', 'Comunicación y Lenguaje'])
    subjects.push(
      availableSubjects.find((s: { name: string }) => s.name === name)?.id ??
        (await post(page, '/api/v1/academic/subjects', { code: name.slice(0, 2) + suffix, name }))
          .id,
    );
  await post(page, `/api/v1/years/${yearId}/courses`, {
    gradeLevelId: grade,
    sectionId: null,
    subjectIds: subjects,
  });
  const courses = await (
    await page.request.get(`/api/v1/years/${yearId}/courses?gradeId=${grade}`)
  ).json();
  const student = (
    await post(page, '/api/v1/people', {
      givenNames: 'Carlos',
      familyNames: 'Méndez · sintético ' + suffix,
      studentCode: 'REF-' + suffix,
      contacts: [],
      isGuardian: false,
      confirmDuplicate: true,
    })
  ).id;
  await post(page, '/api/v1/enrollments', {
    studentId: student,
    academicYearId: yearId,
    gradeLevelId: grade,
    sectionId: null,
    enrolledOn: null,
  });
  const username = 'reference-' + suffix;
  const invitation = await post(page, '/api/v1/access/invitations', {
    username,
    profile: 'basic',
    reason,
  });
  await post(page, `/api/v1/people/${student}/account`, {
    userId: invitation.userId,
    version: (await (await page.request.get(`/api/v1/people/${student}`)).json()).version,
    reason,
  });
  const titles = [
    'Entrega de proyecto',
    'Evaluación parcial',
    'Actividad en clase',
    'Informe de lectura',
    'Práctica de repaso',
  ];
  for (let i = 0; i < titles.length; i++) {
    const course = courses[i % 3].id;
    const assignment = (
      await post(page, `/api/v1/courses/${course}/assignments`, {
        title: titles[i],
        instructions: 'Actividad sintética para validar el panel.',
        dueAt: new Date(now.getTime() + (i + 1) * 864e5).toISOString(),
        allowLate: true,
      })
    ).id;
    const items = await (await page.request.get(`/api/v1/courses/${course}/assignments`)).json();
    await post(page, `/api/v1/courses/${course}/assignments/${assignment}/publish`, {
      version: items.find((a: { id: string }) => a.id === assignment).version,
    });
  }
  for (const title of [
    'Reunión de familias',
    'Información de inscripciones',
    'Actividades de nuestra comunidad',
  ]) {
    const id = (
      await post(page, '/api/v1/announcements', {
        title,
        body: 'Comunicado sintético para verificar la lectura y presentación del panel.',
        audienceKind: 'Grade',
        academicYearId: yearId,
        audienceId: grade,
        forStudents: true,
        forGuardians: false,
        forStaff: false,
      })
    ).id;
    const items = await (await page.request.get('/api/v1/announcements')).json();
    await post(page, `/api/v1/announcements/${id}/publish`, {
      version: items.find((a: { id: string }) => a.id === id).version,
    });
  }
  // Reuse this context so mobile testing also uses the mobile viewport and input behavior.
  const password = 'Test!Aa1-' + randomBytes(20).toString('base64url');
  await page.goto('/Identity/Account/Activate');
  await page.getByLabel('Código de acceso', { exact: true }).fill(invitation.code);
  await page.getByLabel('Nueva contraseña', { exact: true }).fill(password);
  await page.getByLabel('Confirmar contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Usuario', { exact: true }).fill(username);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(page.getByRole('heading', { name: '¡Hola, Carlos!' })).toBeVisible();
  await expect(page.locator('.reference-metric').nth(0)).toContainText('3');
  await expect(page.locator('.reference-metric').nth(1)).toContainText('5');
  await expect(page.locator('.reference-notice-list li')).toHaveCount(3);
  const dashboard = await (await page.request.get(`/api/v1/dashboard?year=${yearId}`)).json();
  expect(dashboard.students[0].courseItems).toHaveLength(3);
  expect(dashboard.students[0].tasks).toHaveLength(5);
  expect(dashboard.academic).toBeNull();
  expect(dashboard.students[0].balance).toBeNull();
  await page.goto(`/workspace?year=${yearId}`);
  for (const width of [320, 390, 900, 1440, 1920]) {
    await page.setViewportSize({ width, height: 1000 });
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
      `dashboard overflow ${width}`,
    ).toBeTruthy();
    expect(
      (await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze())
        .violations,
    ).toEqual([]);
    await page.screenshot({
      path: testInfo.outputPath(`reference-student-${width}.png`),
      fullPage: true,
    });
    if (width === 900)
      await page.screenshot({ path: testInfo.outputPath('student-reference-viewport.png') });
  }
  const month = (await page.locator('#calendar-month').textContent())!;
  await page.getByRole('button', { name: 'Mes siguiente' }).click();
  await expect(page.locator('#calendar-month')).not.toHaveText(month);
  await page.getByRole('button', { name: 'Mes anterior' }).click();
  await expect(page.locator('#calendar-month')).toHaveText(month);
  const dateButton = page.locator(`[data-calendar-date="${today}"]`);
  await dateButton.click();
  await expect(page.locator('.reference-activities')).toContainText(
    'Un día sin pendientes registrados',
  );
  await dateButton.press('ArrowRight');
  await expect(page.locator('.calendar-days button:focus')).toHaveAttribute(
    'data-calendar-date',
    new Date(new Date(today + 'T12:00:00Z').getTime() + 864e5).toISOString().slice(0, 10),
  );
  await page.keyboard.press('Enter');
  await expect(page.locator('.reference-activities')).toContainText('Entrega de proyecto');
  await page.getByRole('button', { name: 'Ver todas las próximas actividades' }).click();
  await expect(page.locator('.reference-activity-list > li')).toHaveCount(3);
  await page
    .locator('.reference-shortcuts')
    .getByRole('link', { name: 'Mis clases', exact: true })
    .click();
  await expect(page.locator('#personal-courses')).toContainText('Ciencias Naturales');
  await expect(page).toHaveURL(new RegExp(`year=${yearId}#personal-courses$`));
  await page
    .locator('.reference-shortcuts')
    .getByRole('link', { name: 'Mis tareas', exact: true })
    .click();
  await expect(page).toHaveURL(
    new RegExp(`/workspace/my/students/${student}\\?year=${yearId}#tasks-title$`),
  );
  await expect(page.getByRole('heading', { name: 'Tareas', exact: true })).toBeVisible();
  await expect(page.locator('.task-list')).toContainText('Entrega de proyecto');
});
