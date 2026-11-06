import { test, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomBytes } from 'node:crypto';
import { courseFixture, post, reason } from './support/school';

async function signIn(page: Page, username: string, password: string) {
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Usuario', { exact: true }).fill(username);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
  await expect(page.getByRole('heading', { name: /^¡Hola,/ })).toBeVisible();
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

test('private course chat notifies on login, retries once, keeps read receipts and separates family conversations', async ({
  page,
  browser,
  baseURL,
}, testInfo) => {
  test.setTimeout(120000);
  const f = await courseFixture(page, 2095);
  const errors: string[] = [];
  const password = 'Qa!Aa1-' + randomBytes(20).toString('base64url');
  const contexts = [];
  async function account(person: string, prefix: string) {
    const username = `${prefix}-${f.suffix}`;
    const invite = await post(page, '/api/v1/access/invitations', {
      username,
      profile: 'basic',
      reason,
    });
    const detail = await (await page.request.get(`/api/v1/people/${person}`)).json();
    await post(page, `/api/v1/people/${person}/account`, {
      version: detail.version,
      userId: invite.userId,
      reason,
    });
    const context = await browser.newContext({
      baseURL,
      viewport: page.viewportSize()!,
      isMobile: testInfo.project.name === 'mobile',
      hasTouch: testInfo.project.name === 'mobile',
    });
    contexts.push(context);
    const p = await context.newPage();
    p.on('pageerror', (e) => errors.push(e.message));
    await p.goto('/Identity/Account/Activate');
    await p.getByLabel('Código de acceso', { exact: true }).fill(invite.code);
    await p.getByLabel('Nueva contraseña', { exact: true }).fill(password);
    await p.getByLabel('Confirmar contraseña', { exact: true }).fill(password);
    await p.getByRole('button', { name: 'Guardar mi contraseña' }).click();
    return { p, username };
  }
  try {
    const teacher = (
      await post(page, '/api/v1/people', {
        givenNames: 'Elena',
        familyNames: 'Docente ' + f.suffix,
        contacts: [],
        isGuardian: false,
        confirmDuplicate: true,
      })
    ).id;
    await post(page, `/api/v1/people/${teacher}/roles`, {
      version: (await (await page.request.get(`/api/v1/people/${teacher}`)).json()).version,
      role: 'staff',
      position: 'Docente',
    });
    await post(page, `/api/v1/courses/${f.course}/teachers`, {
      staffId: teacher,
      role: 'Lead',
      startsOn: '2095-01-10',
      reason,
    });
    const parent = (
      await post(page, '/api/v1/people', {
        givenNames: 'Marta',
        familyNames: 'Familia ' + f.suffix,
        contacts: [],
        isGuardian: true,
        confirmDuplicate: true,
      })
    ).id;
    const relationship = await post(page, '/api/v1/relationships', {
      studentId: f.students[0],
      guardianId: parent,
      kind: 'Mother',
      startsOn: null,
      faculties: ['Communications'],
      reason,
    });
    const student = await account(f.students[0], 'chat-st');
    const teacherUser = await account(teacher, 'chat-tc');
    const parentUser = await account(parent, 'chat-pa');
    await signIn(student.p, student.username, password);
    await student.p.goto(`/workspace/messages?year=${f.yearId}`);
    await student.p
      .getByRole('button', { name: 'Nueva conversación', exact: true })
      .first()
      .click();
    await expect(
      student.p.getByRole('dialog').getByRole('button', { name: /Elena Docente/ }),
    ).toBeVisible();
    await expect(
      student.p.getByRole('dialog').getByRole('button', { name: /Marta Familia/ }),
    ).toHaveCount(0);
    await accessible(student.p);
    await student.p.screenshot({ path: testInfo.outputPath('chat-contacts.png'), fullPage: true });
    await student.p
      .getByRole('dialog')
      .getByRole('button', { name: /Elena Docente/ })
      .click();
    await expect(student.p.getByLabel('Escribe un mensaje', { exact: true })).toBeVisible();
    if (testInfo.project.name === 'mobile') {
      await expect(student.p.locator('.chat-composer')).toBeInViewport({ ratio: 1 });
    }
    const composer = student.p.getByLabel('Escribe un mensaje', { exact: true });
    await composer.fill('Hola, ¿podemos revisar mi tarea?');
    let acknowledgmentLost = false;
    await student.p.route('**/chat/conversations/*/messages', async (route) => {
      if (route.request().method() === 'POST') await route.abort('failed');
      else await route.continue();
    });
    await student.p.getByRole('button', { name: 'Enviar mensaje', exact: true }).click();
    await expect(student.p.getByRole('button', { name: 'Reintentar envío' })).toBeVisible();
    await expect(composer).toHaveValue('Hola, ¿podemos revisar mi tarea?');
    await student.p.unroute('**/chat/conversations/*/messages');
    await student.p.getByRole('button', { name: 'Reintentar envío' }).click();
    await expect(student.p.locator('.chat-bubble')).toHaveCount(1);
    await expect(composer).toHaveValue('');
    const conversation = new URL(student.p.url()).searchParams.get('conversation')!;
    await signIn(teacherUser.p, teacherUser.username, password);
    await expect(teacherUser.p.locator('.sp-toast')).toContainText('Tienes un mensaje sin leer');
    await teacherUser.p.getByRole('link', { name: /^Mensajes: 1 sin leer/ }).click();
    await teacherUser.p.locator('.chat-thread').filter({ hasText: 'Ana' }).click();
    await expect(
      teacherUser.p
        .locator('.chat-history')
        .getByText('Hola, ¿podemos revisar mi tarea?', { exact: true }),
    ).toBeVisible();
    await expect(student.p.locator('.chat-bubble .read')).toHaveText('✓✓ Leído', {
      timeout: 15000,
    });
    await teacherUser.p
      .getByLabel('Escribe un mensaje', { exact: true })
      .fill('Claro, mañana lo vemos en clase.');
    await teacherUser.p.getByRole('button', { name: 'Enviar mensaje', exact: true }).click();
    await expect(
      student.p
        .locator('.chat-history')
        .getByText('Claro, mañana lo vemos en clase.', { exact: true }),
    ).toBeVisible({ timeout: 15000 });
    // The write commits, the automatic poll confirms it, then its HTTP acknowledgment is lost.
    await student.p.route('**/chat/conversations/*/messages', async (route) => {
      if (route.request().method() !== 'POST') {
        await route.continue();
        return;
      }
      await route.fetch();
      await new Promise((resolve) => setTimeout(resolve, 4500));
      await route.abort('failed');
      acknowledgmentLost = true;
    });
    await composer.fill('Confirmación aunque se pierda la respuesta.');
    await student.p.getByRole('button', { name: 'Enviar mensaje', exact: true }).click();
    await expect(
      student.p
        .locator('.chat-bubble')
        .filter({ hasText: 'Confirmación aunque se pierda la respuesta.' }),
    ).toHaveCount(1, { timeout: 15000 });
    await expect(composer).toBeEditable({ timeout: 15000 });
    await expect(composer).toHaveValue('');
    await composer.fill('Borrador del siguiente mensaje');
    await expect.poll(() => acknowledgmentLost, { timeout: 15000 }).toBe(true);
    await expect(composer).toHaveValue('Borrador del siguiente mensaje');
    await expect(student.p.getByRole('button', { name: 'Reintentar envío' })).toHaveCount(0);
    await student.p.unroute('**/chat/conversations/*/messages');
    await composer.fill('');
    await accessible(student.p);
    await student.p.screenshot({
      path: testInfo.outputPath('chat-conversation.png'),
      fullPage: true,
    });
    await accessible(teacherUser.p);
    await teacherUser.p.screenshot({
      path: testInfo.outputPath('chat-teacher.png'),
      fullPage: true,
    });
    await signIn(parentUser.p, parentUser.username, password);
    expect(
      (
        await parentUser.p.request.get(
          `/api/v1/years/${f.yearId}/chat/conversations/${conversation}/messages`,
        )
      ).status(),
    ).toBe(404);
    await parentUser.p.goto(`/workspace/messages?year=${f.yearId}`);
    await parentUser.p
      .getByRole('button', { name: 'Nueva conversación', exact: true })
      .first()
      .click();
    await parentUser.p
      .getByRole('dialog')
      .getByRole('button', { name: /Elena Docente/ })
      .click();
    await parentUser.p
      .getByLabel('Escribe un mensaje', { exact: true })
      .fill('Consulta de la familia.');
    await parentUser.p.getByRole('button', { name: 'Enviar mensaje', exact: true }).click();
    await expect(
      parentUser.p.locator('.chat-history').getByText('Consulta de la familia.', { exact: true }),
    ).toBeVisible();
    await expect(
      parentUser.p.getByText('Hola, ¿podemos revisar mi tarea?', { exact: true }),
    ).toHaveCount(0);
    const familyConversation = new URL(parentUser.p.url()).searchParams.get('conversation')!;
    expect(familyConversation).not.toBe(conversation);
    const relationships = await (await page.request.get(`/api/v1/people/${parent}`)).json();
    // The PeopleService fixture is also exercised through the audited faculty editor API.
    const rel = relationships.students.find((r: { id: string }) => r.id === relationship.id);
    expect(rel).toBeTruthy();
    await post(page, `/api/v1/relationships/${relationship.id}`, {
      version: rel.version,
      kind: 'Mother',
      faculties: [],
      reason,
    });
    await expect(parentUser.p.locator('.chat-bubble')).toHaveCount(0, { timeout: 15000 });
    await expect(
      parentUser.p.getByRole('heading', { name: 'Conversación no disponible' }),
    ).toBeVisible();
    expect(errors).toEqual([]);
  } finally {
    for (const context of contexts) await context.close();
  }
});
