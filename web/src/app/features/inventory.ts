import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { AccessApi, PageResult } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { PendingChanges } from '../core/pending-changes';
import { SessionService } from '../core/session';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

export interface InventoryProduct {
  id: string;
  code: string;
  name: string;
  category: string;
  location: string;
  minimum: number;
  onHand: number;
  isActive: boolean;
  lowStock: boolean;
  version: string;
}
interface Movement {
  id: string;
  sequence: number;
  kind: string;
  delta: number;
  before: number;
  after: number;
  reference: string;
  reason: string;
  occurredAt: string;
  actor: string;
}
interface Summary {
  activeProducts: number;
  lowStockProducts: number;
  units: number;
}
type Mode = 'new' | 'edit' | 'move';

@Component({ imports: [Shell, Icon, FormsModule], templateUrl: './inventory.html' })
export class Inventory implements PendingChanges {
  readonly api = inject(AccessApi);
  readonly session = inject(SessionService);
  private readonly http = inject(HttpClient);
  private readonly alerts = inject(Alerts);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  @ViewChild('editor') private editor!: ElementRef<HTMLDialogElement>;
  readonly rows = signal<InventoryProduct[]>([]);
  readonly summary = signal<Summary | null>(null);
  readonly selected = signal<InventoryProduct | null>(null);
  readonly movements = signal<Movement[]>([]);
  readonly total = signal(0);
  readonly movementTotal = signal(0);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly exporting = signal(false);
  readonly error = signal('');
  readonly dialogError = signal('');
  readonly mode = signal<Mode>('new');
  readonly pending = signal<{ url: string; body: object } | null>(null);
  readonly kindLabels: Record<string, string> = {
    In: 'Entrada',
    Out: 'Salida',
    Count: 'Conteo físico',
  };
  search = '';
  status = 'active';
  low = false;
  page = 1;
  movementPage = 1;
  form = {
    code: '',
    name: '',
    category: '',
    location: '',
    minimum: 0,
    initialQuantity: 0,
    isActive: true,
    reason: '',
  };
  movement = { kind: 'In', quantity: 1, reference: '', reason: '' };
  private requestId = '';
  private baseline = '';
  private generation = 0;
  private historyGeneration = 0;
  private formProduct: InventoryProduct | null = null;
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe(() => void this.load());
  }
  canManage(): boolean {
    return this.session.can('inventory.manage');
  }
  canMove(): boolean {
    return this.session.can('inventory.move');
  }
  private query(): string {
    return new URLSearchParams({
      search: this.search,
      status: this.status,
      low: String(this.low),
    }).toString();
  }
  async load(): Promise<void> {
    const generation = ++this.generation;
    ++this.historyGeneration;
    const params = this.route.snapshot.queryParamMap;
    this.search = params.get('search') ?? '';
    this.status = params.get('status') ?? 'active';
    this.low = params.get('low') === 'true';
    this.page = Number(params.get('page') ?? 1);
    this.loading.set(true);
    this.error.set('');
    this.rows.set([]);
    this.summary.set(null);
    this.selected.set(null);
    this.movements.set([]);
    this.movementPage = 1;
    const id = params.get('product');
    try {
      const [list, summary, detail, history] = await Promise.all([
        this.api.get<PageResult<InventoryProduct>>(
          '/api/v1/inventory/products?' + this.query() + '&page=' + this.page,
        ),
        this.api.get<Summary>('/api/v1/inventory/summary'),
        id
          ? this.api.get<InventoryProduct>('/api/v1/inventory/products/' + id)
          : Promise.resolve(null),
        id
          ? this.api.get<PageResult<Movement>>('/api/v1/inventory/products/' + id + '/movements')
          : Promise.resolve(null),
      ]);
      if (generation !== this.generation) return;
      this.rows.set(list.items);
      this.total.set(list.total);
      this.summary.set(summary);
      this.selected.set(detail);
      this.movements.set(history?.items ?? []);
      this.movementTotal.set(history?.total ?? 0);
    } catch (error) {
      if (generation === this.generation) this.error.set(this.api.message(error));
    } finally {
      if (generation === this.generation) this.loading.set(false);
    }
  }
  filter(page = 1): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        search: this.search || null,
        status: this.status,
        low: this.low || null,
        page: page === 1 ? null : page,
      },
    });
  }
  choose(product: InventoryProduct | null): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { product: product?.id ?? null },
      queryParamsHandling: 'merge',
    });
  }
  async history(page: number): Promise<void> {
    const product = this.selected();
    if (!product) return;
    const generation = ++this.historyGeneration;
    try {
      const result = await this.api.get<PageResult<Movement>>(
        `/api/v1/inventory/products/${product.id}/movements?page=${page}`,
      );
      if (generation !== this.historyGeneration || this.selected()?.id !== product.id) return;
      this.movementPage = page;
      this.movements.set(result.items);
      this.movementTotal.set(result.total);
    } catch (error) {
      if (generation === this.historyGeneration) this.alerts.error(this.api.message(error));
    }
  }
  open(mode: Mode, kind = 'In'): void {
    this.mode.set(mode);
    this.formProduct = this.selected();
    const product = mode === 'edit' ? this.formProduct : null;
    this.form = {
      code: product?.code ?? '',
      name: product?.name ?? '',
      category: product?.category ?? '',
      location: product?.location ?? '',
      minimum: product?.minimum ?? 0,
      initialQuantity: 0,
      isActive: product?.isActive ?? true,
      reason:
        mode === 'new' ? 'Registro inicial de artículo.' : 'Actualización de ficha del artículo.',
    };
    this.movement = {
      kind,
      quantity: kind === 'Count' ? (this.formProduct?.onHand ?? 0) : 1,
      reference: '',
      reason:
        kind === 'In'
          ? 'Ingreso de artículos a bodega.'
          : kind === 'Out'
            ? 'Entrega de artículos de bodega.'
            : 'Conteo físico de existencias.',
    };
    this.requestId = crypto.randomUUID();
    this.pending.set(null);
    this.dialogError.set('');
    this.baseline = this.snapshot();
    this.editor.nativeElement.showModal();
  }
  title(): string {
    return this.mode() === 'new'
      ? 'Nuevo artículo'
      : this.mode() === 'edit'
        ? 'Editar artículo'
        : this.kindLabels[this.movement.kind];
  }
  projected(): number {
    const current = this.formProduct?.onHand ?? 0;
    return this.movement.kind === 'In'
      ? current + Number(this.movement.quantity)
      : this.movement.kind === 'Out'
        ? current - Number(this.movement.quantity)
        : Number(this.movement.quantity);
  }
  available(): number {
    return this.formProduct?.onHand ?? 0;
  }
  productLabel(): string {
    return this.formProduct ? this.formProduct.code + ' · ' + this.formProduct.name : '';
  }
  private snapshot(): string {
    return JSON.stringify(this.mode() === 'move' ? this.movement : this.form);
  }
  async canLeave(): Promise<boolean> {
    if (this.busy()) return false;
    if (!this.editor?.nativeElement.open || (!this.pending() && this.baseline === this.snapshot()))
      return true;
    return this.alerts.confirm({
      title: this.pending() ? 'Hay un registro sin confirmar' : 'Hay cambios sin guardar',
      text: this.pending()
        ? 'El registro podría haberse guardado. Revisa el historial antes de volver a realizar la misma operación. ¿Quieres salir?'
        : '¿Quieres salir y descartar los cambios del formulario?',
      confirmText: 'Salir',
    });
  }
  async close(): Promise<void> {
    if (!(await this.canLeave())) return;
    const uncertain = !!this.pending();
    this.pending.set(null);
    this.editor.nativeElement.close();
    if (uncertain) void this.load();
  }
  async save(): Promise<void> {
    if (this.busy()) return;
    const product = this.formProduct;
    let intent = this.pending();
    if (!intent) {
      const url =
        '/api/v1/inventory/products' +
        (this.mode() === 'new'
          ? ''
          : '/' +
            product!.id +
            (this.mode() === 'move'
              ? this.movement.kind === 'Count'
                ? '/counts'
                : '/movements'
              : ''));
      const body =
        this.mode() === 'move'
          ? { ...this.movement, requestId: this.requestId, version: product!.version }
          : this.mode() === 'new'
            ? { ...this.form, requestId: this.requestId }
            : { ...this.form, version: product!.version };
      intent = { url, body: structuredClone(body) };
      this.pending.set(intent);
    }
    this.busy.set(true);
    this.dialogError.set('');
    let timer: ReturnType<typeof setTimeout> | undefined;
    try {
      await Promise.race([
        this.api.post(intent.url, intent.body),
        new Promise((_, reject) => {
          timer = setTimeout(() => reject(new Error('timeout')), 20000);
        }),
      ]);
      this.pending.set(null);
      this.editor.nativeElement.close();
      this.alerts.success('Inventario actualizado');
      void this.load();
    } catch (error) {
      if (
        error instanceof HttpErrorResponse &&
        [400, 401, 403, 404, 409, 429].includes(error.status)
      )
        this.pending.set(null);
      this.dialogError.set(
        this.pending()
          ? 'No se pudo confirmar el registro. Reintenta: se conserva el mismo intento para evitar duplicados.'
          : this.api.message(error),
      );
    } finally {
      clearTimeout(timer);
      this.busy.set(false);
    }
  }
  async refreshEditor(): Promise<void> {
    if (this.pending() || this.busy() || !this.formProduct) return;
    if (
      this.mode() === 'edit' &&
      !(await this.alerts.confirm({
        title: 'Recargar ficha del artículo',
        text: 'Se traerán los datos actuales. Los cambios de este formulario se descartarán para que puedas revisarlos.',
        confirmText: 'Recargar ficha',
      }))
    )
      return;
    this.busy.set(true);
    try {
      this.formProduct = await this.api.get<InventoryProduct>(
        '/api/v1/inventory/products/' + this.formProduct.id,
      );
      if (this.mode() === 'edit')
        this.form = { ...this.form, ...this.formProduct, initialQuantity: 0 };
      this.dialogError.set('');
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
  async export(): Promise<void> {
    this.exporting.set(true);
    try {
      const blob = await firstValueFrom(
        this.http.get('/api/v1/inventory/export?' + this.query(), { responseType: 'blob' }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'inventario-bodega.csv';
      anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (error) {
      this.alerts.error(this.api.message(error));
    } finally {
      this.exporting.set(false);
    }
  }
}
