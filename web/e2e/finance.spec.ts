import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';

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
  expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze()).violations).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
}

test('charge tuition by preview, collect with a receipt and show clearance and the guardian statement', async ({ page, browser }, testInfo) => {
  test.setTimeout(60_000);
  const owner = process.env['SCHOOLPLATFORM_E2E_USERNAME']!;
  const ownerPassword = process.env['SCHOOLPLATFORM_E2E_PASSWORD']!;
  const suffix = randomUUID().slice(0, 5).toUpperCase();
  const calendarYear = 2070 + testInfo.repeatEachIndex * 2 + (testInfo.project.name === 'mobile' ? 1 : 0);
  const code = 'FIN-' + suffix;
  const conceptName = 'Colegiatura ' + suffix;
  const guardianUser = 'fin-guardian-' + suffix.toLowerCase();
  const guardianPassword = 'Guardian-Synthetic!12345';

  await login(page, owner, ownerPassword);
  const session = await (await page.request.get('/api/v1/session')).json();
  if (!session.permissions.includes('finance.charges')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((x: { id: string }) => x.id === session.userId);
    await post(page, `/api/v1/access/accounts/${me.id}/profile`, { version: me.version, reason, profile: 'administrator' });
    await login(page, owner, ownerPassword);
  }

  // Synthetic school structure through the API (covered in its own spec).
  const year = (await post(page, '/api/v1/academic/years', { label: `${calendarYear}-${suffix}`, startsOn: `${calendarYear}-01-10`, endsOn: `${calendarYear}-10-30` })).id;
  await post(page, `/api/v1/academic/years/${year}/periods`, { name: null, startsOn: `${calendarYear}-01-10`, endsOn: `${calendarYear}-03-10` });
  const detail = await (await page.request.get(`/api/v1/academic/years/${year}`)).json();
  await post(page, `/api/v1/academic/years/${year}/status`, { version: detail.year.version, status: 'EnrollmentOpen', reason });
  const grade = (await post(page, '/api/v1/academic/grades', { code: 'F' + suffix, name: 'Grado finanzas ' + suffix, stage: '', sequence: 50 })).id;
  const student = (await post(page, '/api/v1/people', { givenNames: 'Elisa', familyNames: 'Sintética ' + suffix, contacts: [], studentCode: code, isGuardian: false, confirmDuplicate: true })).id;
  await post(page, '/api/v1/enrollments', { studentId: student, academicYearId: year, gradeLevelId: grade, sectionId: null, enrolledOn: `${calendarYear}-01-10` });
  const guardian = (await post(page, '/api/v1/people', { givenNames: 'Irene', familyNames: 'Sintética ' + suffix, contacts: [], studentCode: null, isGuardian: true, confirmDuplicate: true })).id;
  await post(page, '/api/v1/relationships', { studentId: student, guardianId: guardian, kind: 'Mother', startsOn: null, faculties: ['Finance'], reason });
  const invite = await post(page, '/api/v1/access/invitations', { username: guardianUser, profile: 'basic', reason });
  const guardianDetail = await (await page.request.get(`/api/v1/people/${guardian}`)).json();
  await post(page, `/api/v1/people/${guardian}/account`, { version: guardianDetail.version, userId: invite.userId, reason });

  // Finance configuration in the UI
  await page.goto(`/workspace/finance/setup?year=${year}`);
  await expect(page.getByRole('heading', { name: 'Conceptos, cuotas y cargos' })).toBeVisible();
  await page.getByRole('region', { name: 'Conceptos' }).getByRole('button', { name: '+ Nuevo' }).click();
  await page.getByRole('dialog').getByLabel('Código').fill('C' + suffix);
  await page.getByRole('dialog').getByLabel('Nombre').fill(conceptName);
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.locator('.sp-toast')).toBeVisible();
  await page.getByRole('button', { name: '+ Agregar' }).click();
  await page.getByRole('dialog').getByLabel('Nombre de la cuota').fill('Cuota 1');
  await page.getByRole('dialog').getByLabel('Vencimiento').fill(`${calendarYear}-02-05`);
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.locator('.period-list')).toContainText('Cuota 1');
  await page.getByRole('region', { name: 'Cortes de solvencia' }).getByRole('button', { name: '+ Nuevo' }).click();
  await page.getByRole('dialog').getByLabel('Nombre del corte').fill('Primer corte');
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.getByRole('region', { name: 'Cortes de solvencia' })).toContainText('Primer corte');

  await page.getByRole('combobox', { name: 'Concepto' }).selectOption({ label: conceptName });
  await page.getByRole('combobox', { name: 'Cuota' }).selectOption({ index: 1 });
  await page.getByLabel(/^Importe/).fill('450');
  await page.getByRole('checkbox', { name: 'Grado finanzas ' + suffix }).check();
  await page.getByRole('button', { name: 'Ver vista previa' }).click();
  await expect(page.locator('.preview-summary')).toContainText('1 por emitir');
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('finance-setup.png'), fullPage: true });
  await page.getByRole('button', { name: /Confirmar emisión de 1 cargo/ }).click();
  await expect(page.locator('.sp-toast')).toContainText('Cargos emitidos: 1');

  // Clearance before payment
  await page.goto(`/workspace/finance/clearance?year=${year}`);
  await expect(page.locator('tbody')).toContainText('Con saldo pendiente');

  // Cashier
  await page.goto('/workspace/finance');
  await page.getByRole('searchbox').fill(code);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.getByRole('button', { name: new RegExp('Elisa Sintética ' + suffix) }).click();
  await expect(page.getByRole('heading', { name: new RegExp('Elisa Sintética ' + suffix) })).toBeVisible();
  await page.getByRole('button', { name: 'Registrar pago' }).click();
  await page.getByRole('dialog').getByLabel('Nombre de quien paga').fill('Irene Sintética ' + suffix);
  await page.getByRole('dialog').getByRole('combobox', { name: 'Medio de pago' }).selectOption('Transfer');
  await page.getByRole('dialog').getByLabel(/^Referencia/).fill('TRX-' + suffix);
  await expect(page.locator('.allocation-total')).toContainText('450');
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('payment-dialog.png'), fullPage: true });
  await page.getByRole('button', { name: 'Confirmar pago' }).click();
  await expect(page.locator('.sp-alert')).toContainText('Pago registrado');
  await expectAccessible(page);
  await page.getByRole('button', { name: 'Listo' }).click();
  await expect(page.locator('.balance-box')).toContainText('0.00');
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('statement.png'), fullPage: true });
  await page.getByRole('link', { name: /^Recibo \d+$/ }).first().click();
  await expect(page.getByRole('heading', { name: 'Recibo de pago' })).toBeVisible();
  await expect(page.locator('.receipt-lines')).toContainText('Elisa Sintética ' + suffix);
  await expect(page.locator('.receipt-header img')).toHaveJSProperty('complete', true);
  expect(await page.locator('.receipt-header img').evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);
  await expectAccessible(page);
  await page.screenshot({ path: testInfo.outputPath('receipt.png'), fullPage: true });

  await page.goto(`/workspace/finance/clearance?year=${year}`);
  await expect(page.locator('tbody')).toContainText('Solvente');

  // Guardian with the Finance faculty sees the statement and the receipt
  const guardianContext = await browser.newContext({ baseURL: new URL(page.url()).origin });
  const guardianPage = await guardianContext.newPage();
  await guardianPage.goto('/Identity/Account/Activate');
  await guardianPage.getByLabel('Código de acceso', { exact: true }).fill(invite.code);
  await guardianPage.getByLabel('Nueva contraseña', { exact: true }).fill(guardianPassword);
  await guardianPage.getByLabel('Confirmar contraseña', { exact: true }).fill(guardianPassword);
  await guardianPage.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await login(guardianPage, guardianUser, guardianPassword);
  await guardianPage.getByRole('link', { name: 'Elisa Sintética ' + suffix }).click();
  await expect(guardianPage.getByRole('heading', { name: 'Estado de cuenta' })).toBeVisible();
  await expect(guardianPage.locator('.balance-box')).toContainText('0.00');
  await expectAccessible(guardianPage);
  await guardianPage.screenshot({ path: testInfo.outputPath('guardian-statement.png'), fullPage: true });
  await guardianPage.getByRole('link', { name: /Ver recibo/ }).click();
  await expect(guardianPage.getByRole('heading', { name: 'Recibo de pago' })).toBeVisible();
  expect((await guardianPage.request.get('/api/v1/finance/payments')).status()).toBe(403);

  // An authorized reversal restores the debt in both cashier and family views.
  await page.goto(`/workspace/finance?student=${student}`);
  await page.getByRole('button', { name: /^Revertir recibo/ }).click();
  await page.getByRole('dialog').getByLabel('Motivo y autorización').fill(reason);
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page.locator('.balance-box')).toContainText('450.00');
  await expect(page.locator('.payment-list .reversed')).toHaveCount(1);
  await guardianPage.goBack();
  await expect(guardianPage.locator('.balance-box')).toContainText('450.00');
  await page.goto(`/workspace/finance/clearance?year=${year}`);
  await expect(page.locator('tbody')).toContainText('Con saldo pendiente');

  // An excessive adjustment must leave the form and balance intact; a valid discount reduces the debt.
  await page.goto(`/workspace/finance?student=${student}`);
  await page.getByRole('button', { name: /^Ajustar / }).click();
  await page.getByRole('dialog').getByRole('combobox', { name: 'Tipo', exact: true }).selectOption('Discount');
  await page.getByRole('dialog').getByLabel('Monto', { exact: true }).fill('1000');
  await page.getByRole('dialog').getByLabel('Motivo y autorización').fill(reason);
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('dialog').getByRole('alert')).toContainText(/saldo/);
  await expect(page.locator('.balance-box')).toContainText('450.00');
  await page.getByRole('dialog').getByLabel('Monto', { exact: true }).fill('100');
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page.locator('.balance-box')).toContainText('350.00');
  await guardianPage.reload();
  await expect(guardianPage.locator('.balance-box')).toContainText('350.00');

  // A replacement payment covers only the remainder; the reversed receipt stays in the history.
  await page.getByRole('button', { name: 'Registrar pago', exact: true }).click();
  await page.getByRole('dialog').getByLabel('Nombre de quien paga').fill('Irene Sintética ' + suffix);
  await expect(page.locator('.allocation-total')).toContainText('350');
  await page.getByRole('button', { name: 'Confirmar pago' }).click();
  await expect(page.locator('.sp-alert')).toContainText('Pago registrado');
  await page.getByRole('button', { name: 'Listo', exact: true }).click();
  await expect(page.locator('.balance-box')).toContainText('0.00');
  await expect(page.locator('.payment-list .reversed')).toHaveCount(1);
  await guardianPage.reload();
  await expect(guardianPage.locator('.balance-box')).toContainText('0.00');
  await page.goto(`/workspace/finance/clearance?year=${year}`);
  await expect(page.locator('tbody')).toContainText('Solvente');
  await guardianContext.close();
});
