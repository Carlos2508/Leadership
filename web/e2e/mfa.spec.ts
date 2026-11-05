import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { createHmac, randomUUID } from 'node:crypto';

const reason = 'Autorización institucional sintética para prueba de navegador.';

function totp(formattedKey: string, offset = 0): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = 0;
  let value = 0;
  const bytes: number[] = [];
  for (const c of formattedKey.replace(/\s/g, '').toUpperCase()) {
    value = (value << 5) | alphabet.indexOf(c);
    bits += 5;
    if (bits >= 8) {
      bytes.push((value >>> (bits - 8)) & 0xff);
      bits -= 8;
    }
  }
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30000) + offset));
  const hash = createHmac('sha1', Buffer.from(bytes)).update(counter).digest();
  const o = hash[hash.length - 1] & 0x0f;
  const code = ((hash[o] & 0x7f) << 24) | (hash[o + 1] << 16) | (hash[o + 2] << 8) | hash[o + 3];
  return String(code % 1_000_000).padStart(6, '0');
}

async function login(page: Page, username: string, password: string): Promise<void> {
  await page.goto('/Identity/Account/Login');
  await page.getByLabel('Usuario', { exact: true }).fill(username);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
}

async function expectAccessible(page: Page): Promise<void> {
  expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze()).violations).toEqual([]);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
}

test('a member enables two-step verification and the next login asks for the code', async ({ page, browser }, testInfo) => {
  const username = 'mfa-' + randomUUID().slice(0, 6);
  const password = 'Synthetic-Portal!12345';
  await login(page, process.env['SCHOOLPLATFORM_E2E_USERNAME']!, process.env['SCHOOLPLATFORM_E2E_PASSWORD']!);
  await expect(page.getByRole('heading', { name: /^(Buenos días|Buenas tardes|Buenas noches)/ })).toBeVisible();
  const token = (await (await page.request.get('/api/v1/antiforgery')).json()).token as string;
  const invite = await (await page.request.post('/api/v1/access/invitations', { data: { username, profile: 'basic', reason }, headers: { 'X-XSRF-TOKEN': token } })).json();

  const context = await browser.newContext({ baseURL: new URL(page.url()).origin });
  const member = await context.newPage();
  await member.goto('/Identity/Account/Activate');
  await member.getByLabel('Código de acceso', { exact: true }).fill(invite.code);
  await member.getByLabel('Nueva contraseña', { exact: true }).fill(password);
  await member.getByLabel('Confirmar contraseña', { exact: true }).fill(password);
  await member.getByRole('button', { name: 'Guardar mi contraseña' }).click();
  await login(member, username, password);
  await member.goto('/workspace/security');
  await member.getByRole('button', { name: 'Activar verificación' }).click();
  await expect(member.getByRole('img', { name: /Código QR/ })).toBeVisible();
  const key = (await member.locator('.shared-key').textContent())!.trim();
  await expectAccessible(member);
  await member.screenshot({ path: testInfo.outputPath('mfa-setup.png'), fullPage: true });
  await member.getByRole('textbox', { name: /código de 6 dígitos/ }).fill(totp(key));
  await member.getByRole('button', { name: 'Confirmar y activar' }).click();
  await expect(member.locator('.recovery-codes li')).toHaveCount(10);
  await expect(member.getByText('Activa', { exact: true })).toBeVisible();
  await expectAccessible(member);
  await member.screenshot({ path: testInfo.outputPath('mfa-recovery-codes.png'), fullPage: true });
  await member.getByRole('button', { name: 'Ya los guardé' }).click();

  await context.clearCookies();
  await login(member, username, password);
  await expect(member.getByRole('heading', { name: 'Confirma que eres tú.' })).toBeVisible();
  await expectAccessible(member);
  await member.screenshot({ path: testInfo.outputPath('mfa-challenge.png'), fullPage: true });
  await member.getByLabel('Código de verificación').fill(totp(key, 1));
  await member.getByRole('button', { name: 'Verificar e ingresar' }).click();
  await expect(member.getByRole('heading', { name: /^(Buenos días|Buenas tardes|Buenas noches)/ })).toBeVisible();
  await context.close();
});
