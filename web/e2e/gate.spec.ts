import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';
import { schoolToday } from './school-date';

const reason = 'Verificación sintética de portería y registro de incidencias.';
// Pairing QR codes are credentials; never keep automatic trace screenshots of them.
test.use({ trace: 'off' });
async function login(page: Page, username: string, password: string) {
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Usuario', { exact: true }).fill(username);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(page.getByRole('heading', { name: /^¡Hola,/ })).toBeVisible();
}
async function post(page: Page, path: string, data: object) {
  const token = (await (await page.request.get('/api/v1/antiforgery')).json()).token;
  const response = await page.request.post(path, { data, headers: { 'X-XSRF-TOKEN': token } });
  expect(response.status(), path).toBeLessThan(300);
  return response.status() === 204 ? null : response.json();
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
test('gate reader, cards, manual marks, report, devices and family access', async ({
  page,
  browser,
}, testInfo) => {
  test.setTimeout(60_000);
  await login(
    page,
    process.env['SCHOOLPLATFORM_E2E_USERNAME']!,
    process.env['SCHOOLPLATFORM_E2E_PASSWORD']!,
  );
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('attendance.manage')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const owner = accounts.items.find((a: { id: string }) => a.id === session.userId);
    await post(page, `/api/v1/access/accounts/${owner.id}/profile`, {
      version: owner.version,
      profile: 'administrator',
      reason,
    });
    await login(
      page,
      process.env['SCHOOLPLATFORM_E2E_USERNAME']!,
      process.env['SCHOOLPLATFORM_E2E_PASSWORD']!,
    );
  }
  const s = randomUUID().slice(0, 6).toUpperCase();
  const start = new Date(Date.now() - 86400000 * 2).toISOString().slice(0, 10);
  const end = new Date(Date.now() + 86400000 * 90).toISOString().slice(0, 10);
  const today = await schoolToday(page);
  const years = await (await page.request.get('/api/v1/academic/years')).json();
  const current = years.find(
    (y: { startsOn: string; endsOn: string }) => y.startsOn <= today && y.endsOn >= today,
  );
  const year =
    current?.id ??
    (
      await post(page, '/api/v1/academic/years', {
        label: 'Portería ' + s,
        startsOn: start,
        endsOn: end,
      })
    ).id;
  const detail = await (await page.request.get('/api/v1/academic/years/' + year)).json();
  if (detail.year.status === 'Draft')
    await post(page, `/api/v1/academic/years/${year}/status`, {
      version: detail.year.version,
      status: 'EnrollmentOpen',
      reason,
    });
  const grade = (
    await post(page, '/api/v1/academic/grades', {
      code: 'G' + s,
      name: 'Grado portería ' + s,
      stage: '',
      sequence: 90,
    })
  ).id;
  const student = (
    await post(page, '/api/v1/people', {
      givenNames: 'Sara',
      familyNames: 'Sintética ' + s,
      contacts: [],
      studentCode: 'G-' + s,
      isGuardian: false,
      confirmDuplicate: true,
    })
  ).id;
  await post(page, '/api/v1/enrollments', {
    studentId: student,
    academicYearId: year,
    gradeLevelId: grade,
    sectionId: null,
    enrolledOn: detail.year.startsOn,
  });

  await page.goto('/workspace/people/' + student);
  await expect(page.getByRole('heading', { name: 'Carnet y fotografía' })).toBeVisible();
  const png = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/nJzZFgAAAABJRU5ErkJggg==',
    'base64',
  );
  await page
    .getByLabel('Subir foto privada')
    .setInputFiles({ name: 'synthetic.png', mimeType: 'image/png', buffer: png });
  await expect(page.getByText('La foto privada se guardó.')).toBeVisible();
  const cardPdf = await page.request.get(`/api/v1/students/${student}/card.pdf?layout=card`);
  expect(cardPdf.headers()['content-type']).toContain('application/pdf');
  expect(cardPdf.status()).toBe(200);
  const oldToken = (await (await page.request.get(`/api/v1/students/${student}/card`)).json()).card
    .token;
  await page.getByRole('button', { name: 'Reemplazar carnet', exact: true }).click();
  await page.getByLabel('Motivo del reemplazo').fill(reason);
  await accessible(page);
  await page.getByRole('button', { name: 'Confirmar reemplazo' }).click();
  await expect(page.getByText('Carnet reemplazado. Descarga la nueva impresión.')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('person-card.png'), fullPage: true });
  await expect(page.getByRole('link', { name: 'Carnet en tarjeta', exact: true })).toHaveAttribute(
    'href',
    new RegExp(`/api/v1/people/${student}/card\\.pdf\\?yearId=[0-9a-f-]+&layout=card`),
  );
  const token = (await (await page.request.get(`/api/v1/students/${student}/card`)).json()).card
    .token;

  // Staff get their own card (printed for an explicit cycle); it is never marked at the gate.
  const teacher = (
    await post(page, '/api/v1/people', {
      givenNames: 'Tomás',
      familyNames: 'Personal ' + s,
      contacts: [],
      isGuardian: false,
      staffPosition: 'Docente de Ciencias',
      confirmDuplicate: true,
    })
  ).id;
  await page.goto('/workspace/people/' + teacher);
  await expect(page.getByRole('heading', { name: 'Carnet y fotografía' })).toBeVisible();
  await expect(page.getByText(/Carnet de personal/)).toBeVisible();
  const staffCard = await page.request.get(`/api/v1/people/${teacher}/card.pdf?yearId=${year}&layout=card`);
  expect(staffCard.status()).toBe(200);
  expect(staffCard.headers()['content-type']).toContain('application/pdf');
  await accessible(page);

  await page.goto('/workspace/gate');
  await page.getByLabel('Código del carnet').fill(oldToken);
  await page.getByRole('button', { name: 'Registrar lectura' }).click();
  await expect(page.getByText(/Este carnet fue reemplazado/)).toBeVisible();
  await page.getByLabel('Código del carnet').fill(token);
  await page.getByLabel('Código del carnet').press('Enter');
  await expect(page.getByText('Entrada registrada', { exact: true })).toBeVisible();
  await page.getByLabel('Código del carnet').fill(token);
  await page.getByLabel('Código del carnet').press('Enter');
  await expect(page.getByText(/No se volvió a marcar/)).toBeVisible();
  await accessible(page);
  await page.screenshot({ path: testInfo.outputPath('gate-kiosk.png'), fullPage: true });
  await page.getByLabel('Buscar estudiante').fill('G-' + s);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.getByRole('button', { name: 'Seleccionar G-' + s }).click();
  await page.getByRole('combobox', { name: 'Dirección', exact: true }).selectOption('Out');
  await page.getByLabel('Motivo', { exact: true }).fill(reason);
  await accessible(page);
  await page.getByRole('button', { name: 'Guardar marca' }).click();
  await expect(page.getByText('Salida registrada', { exact: true })).toBeVisible();

  await page.goto('/workspace/gate/report');
  await page.getByLabel('Filtrar por nombre').fill('G-' + s);
  await page.getByRole('button', { name: 'Ver marcas G-' + s }).click();
  await expect(page.getByRole('button', { name: 'Anular salida' })).toBeVisible();
  await page.getByRole('button', { name: 'Anular salida' }).click();
  await page.getByLabel('Motivo de anulación').fill(reason);
  await accessible(page);
  await page.getByRole('button', { name: 'Confirmar anulación' }).click();
  await expect(page.getByText('Anulada: ' + reason)).toBeVisible();
  await page.getByRole('button', { name: 'Cerrar', exact: true }).click();
  await expect(page.getByText('Dentro', { exact: true })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('gate-report.png'), fullPage: true });

  await page.goto('/workspace/gate/devices');
  await page.getByLabel('Nombre del teléfono').fill('Lector ' + s);
  await page.getByRole('button', { name: 'Crear emparejamiento' }).click();
  const qr = page.getByAltText('QR privado de emparejamiento');
  await expect(qr).toBeVisible();
  await expect
    .poll(() => qr.evaluate((img: HTMLImageElement) => img.naturalWidth))
    .toBeGreaterThan(0);
  await page.getByRole('button', { name: 'Terminé de emparejar' }).click();
  await expect(qr).toHaveCount(0);
  await page.getByRole('button', { name: 'Revocar Lector ' + s, exact: true }).click();
  await page.getByLabel('Motivo de revocación').fill(reason);
  await page.getByRole('button', { name: 'Confirmar revocación' }).click();
  await expect(page.getByText('El teléfono ya no puede registrar marcas.')).toBeVisible();
  await accessible(page);
  await page.screenshot({ path: testInfo.outputPath('gate-devices.png'), fullPage: true });

  const invite = await post(page, '/api/v1/access/invitations', {
    username: 'gate-fam-' + s.toLowerCase(),
    profile: 'basic',
    reason,
  });
  const guardian = (
    await post(page, '/api/v1/people', {
      givenNames: 'Alma',
      familyNames: 'Familia ' + s,
      contacts: [],
      isGuardian: true,
      confirmDuplicate: true,
    })
  ).id;
  const person = await (await page.request.get('/api/v1/people/' + guardian)).json();
  await post(page, `/api/v1/people/${guardian}/account`, {
    version: person.version,
    userId: invite.userId,
    reason,
  });
  const link = (
    await post(page, '/api/v1/relationships', {
      studentId: student,
      guardianId: guardian,
      kind: 'Mother',
      startsOn: null,
      faculties: ['AcademicRecords'],
      reason,
    })
  ).id;
  const context = await browser.newContext({
    baseURL: new URL(page.url()).origin,
    viewport: page.viewportSize(),
  });
  const family = await context.newPage();
  await family.goto('/Identity/Account/Activate');
  await family.getByLabel('Código de acceso', { exact: true }).fill(invite.code);
  await family.getByLabel('Nueva contraseña', { exact: true }).fill('Synthetic-Portal!12345');
  await family.getByLabel('Confirmar contraseña', { exact: true }).fill('Synthetic-Portal!12345');
  await family.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await login(family, 'gate-fam-' + s.toLowerCase(), 'Synthetic-Portal!12345');
  await family.goto('/workspace/my/students/' + student);
  await expect(family.getByRole('heading', { name: 'Entradas y salidas' })).toBeVisible();
  await expect(family.getByText('1 marca', { exact: true })).toBeVisible();
  await accessible(family);
  await family.screenshot({ path: testInfo.outputPath('gate-family.png'), fullPage: true });
  expect((await family.request.get('/api/v1/gate/daily')).status()).toBe(403);
  const fresh = await (await page.request.get('/api/v1/people/' + student)).json();
  const relation = fresh.guardians.find((r: { id: string }) => r.id === link);
  await post(page, '/api/v1/relationships/' + link, {
    version: relation.version,
    kind: 'Mother',
    faculties: [],
    reason,
  });
  expect((await family.request.get(`/api/v1/me/students/${student}/gate`)).status()).toBe(404);
  await context.close();

  await page.goto('/workspace/enrollment?year=' + year);
  await page.getByRole('combobox', { name: 'Grado', exact: true }).selectOption(grade);
  await page.getByRole('button', { name: 'Imprimir carnets', exact: true }).click();
  await expect(
    page.getByRole('dialog').getByRole('link', { name: 'Abrir PDF', exact: true }),
  ).toHaveAttribute('href', `/api/v1/years/${year}/cards.pdf?gradeId=${grade}&layout=sheet`);
});
