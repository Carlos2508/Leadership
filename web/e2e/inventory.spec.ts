import { test, expect, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { randomUUID } from 'node:crypto';
import { login, post, reason } from './support/school';

async function inventoryAccess(page: Page) {
  await login(page);
  const session = await (await page.request.get('/api/v1/session')).json();
  if (session.permissions.includes('inventory.manage')) return;
  const permissions = await (await page.request.get('/api/v1/access/permissions')).json();
  const role = await post(page, '/api/v1/access/roles', {
    label: 'Inventario QA ' + randomUUID().slice(0, 7),
    description: 'Configuración sintética',
    permissions: permissions.map((p: { key: string }) => p.key),
    isActive: true,
    reason,
  });
  const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
  const me = accounts.items.find((a: { id: string }) => a.id === session.userId);
  await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
    profile: role.key,
    version: me.version,
    reason,
  });
  await login(page);
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

test('school warehouse records initial stock, safely retries a lost response, counts, exports and archives with history', async ({
  page,
}, info) => {
  test.setTimeout(100000);
  await inventoryAccess(page);
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/workspace/inventory');
  await expect(page.getByRole('heading', { name: 'Inventario', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Nuevo artículo', exact: true }).click();
  const code = ('QA-' + randomUUID().slice(0, 8)).toUpperCase();
  await page.getByLabel('Código del artículo', { exact: true }).fill(code);
  await page.getByLabel('Nombre del artículo', { exact: true }).fill('Cuaderno de prueba ' + code);
  await page.getByLabel('Categoría', { exact: true }).fill('Cuadernos');
  await page.getByLabel('Ubicación', { exact: true }).fill('Estante A');
  await page.getByLabel('Stock mínimo', { exact: true }).fill('5');
  await page.getByLabel('Unidades iniciales', { exact: true }).fill('20');
  await accessible(page);
  await page.getByRole('button', { name: 'Guardar artículo', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await page.locator('.inventory-product').filter({ hasText: code }).click();
  await expect(page.locator('.inventory-balance strong')).toContainText('20');
  if (info.project.name === 'mobile')
    await expect(page.locator('.inventory-detail h2')).toBeInViewport();
  await expect(page.locator('.inventory-history li')).toHaveCount(1);
  await page.getByRole('button', { name: 'Salida', exact: true }).click();
  await page.getByLabel('Cantidad de unidades', { exact: true }).fill('17');
  await page.getByLabel('Referencia (opcional)', { exact: true }).fill('Entrega QA');
  let lost = false;
  await page.route('**/inventory/products/*/movements', async (route) => {
    if (route.request().method() === 'POST' && !lost) {
      lost = true;
      await route.fetch();
      await route.abort('failed');
    } else await route.continue();
  });
  await page.getByRole('button', { name: 'Registrar salida', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('No se pudo confirmar el registro');
  await page.getByRole('button', { name: 'Reintentar registro', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page.locator('.inventory-balance strong')).toContainText('3');
  await expect(page.locator('.inventory-history li')).toHaveCount(2);
  await expect(page.locator('.inventory-balance')).toContainText('Stock bajo');
  await page.unroute('**/inventory/products/*/movements');
  await page.getByRole('button', { name: 'Salida', exact: true }).click();
  await page.getByLabel('Cantidad de unidades', { exact: true }).fill('4');
  await page.getByRole('button', { name: 'Registrar salida', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('No hay suficientes unidades');
  await page.getByLabel('Cantidad de unidades', { exact: true }).fill('1');
  await page.getByRole('button', { name: 'Registrar salida', exact: true }).click();
  await expect(page.locator('.inventory-balance strong')).toContainText('2');
  await page.getByRole('button', { name: 'Conteo físico', exact: true }).click();
  await page.getByLabel('Unidades contadas físicamente', { exact: true }).fill('0');
  await accessible(page);
  await page.screenshot({ path: info.outputPath('inventory-count.png'), fullPage: true });
  await page.getByRole('button', { name: 'Registrar conteo físico', exact: true }).click();
  await expect(page.locator('.inventory-balance strong')).toContainText('0');
  await expect(page.locator('.inventory-history li')).toHaveCount(4);
  await accessible(page);
  await page.screenshot({ path: info.outputPath('inventory-history.png'), fullPage: true });
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Exportar CSV', exact: true }).click();
  expect((await download).suggestedFilename()).toBe('inventario-bodega.csv');
  await page.getByRole('button', { name: 'Editar artículo', exact: true }).click();
  await page.getByRole('checkbox', { name: /Artículo activo/ }).uncheck();
  await page.getByRole('button', { name: 'Guardar artículo', exact: true }).click();
  await expect(page.locator('.inventory-balance')).toContainText('Archivado');
  await expect(page.locator('.inventory-history li')).toHaveCount(4);
  await page.getByLabel('Estado', { exact: true }).selectOption('archived');
  await page.getByLabel('Buscar artículo', { exact: true }).fill(code);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.locator('.inventory-product').filter({ hasText: code }).click();
  await expect(page.locator('.inventory-balance')).toContainText('Archivado');
  expect(errors).toEqual([]);
});

test('inventory never shows stale details after a failed filter or a late response', async ({
  page,
}) => {
  await inventoryAccess(page);
  const code = ('SAFE-' + randomUUID().slice(0, 6)).toUpperCase();
  const p = await post(page, '/api/v1/inventory/products', {
    requestId: randomUUID(),
    code,
    name: 'Artículo sin datos viejos',
    category: '',
    location: '',
    minimum: 0,
    initialQuantity: 7,
    reason,
  });
  await page.goto('/workspace/inventory?product=' + p.id);
  await expect(page.locator('.inventory-balance strong')).toContainText('7');
  await page.route('**/inventory/products?**', async (route) => {
    if (route.request().url().includes('search=fallo'))
      await route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({ title: 'Inventario temporalmente no disponible' }),
      });
    else await route.continue();
  });
  await page.getByLabel('Buscar artículo', { exact: true }).fill('fallo');
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Inventario temporalmente no disponible');
  await expect(page.locator('.inventory-balance')).toHaveCount(0);
  await page.unroute('**/inventory/products?**');
  await page.getByLabel('Buscar artículo', { exact: true }).fill(code);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await expect(page.locator('.inventory-product')).toHaveCount(1);
  await page.route('**/inventory/products?**', async (route) => {
    if (route.request().url().includes('search=lento')) {
      const response = await route.fetch();
      await new Promise((r) => setTimeout(r, 1200));
      await route.fulfill({ response });
    } else await route.continue();
  });
  await page.getByLabel('Buscar artículo', { exact: true }).fill('lento');
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await page.getByLabel('Buscar artículo', { exact: true }).fill(code);
  await page.getByRole('button', { name: 'Buscar', exact: true }).click();
  await expect(page.locator('.inventory-product')).toHaveCount(1);
  await page.waitForTimeout(1500);
  await expect(page.locator('.inventory-product')).toContainText(code);
});
