import { Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccessApi } from '../core/access-api';
import { Alerts } from '../core/alerts';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

interface PermissionDefinition {
  key: string;
  label: string;
  area: string;
}
interface Role {
  key: string;
  label: string;
  description: string;
  isActive: boolean;
  version: string;
  permissions: string[];
  accounts: number;
}
@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  templateUrl: './access-roles.html',
})
export class AccessRoles {
  readonly api = inject(AccessApi);
  readonly alerts = inject(Alerts);
  @ViewChild('editor') private editor!: ElementRef<HTMLDialogElement>;
  readonly roles = signal<Role[]>([]);
  readonly catalog = signal<PermissionDefinition[]>([]);
  readonly areas = computed(() => [...new Set(this.catalog().map((p) => p.area))]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly dialogError = signal('');
  readonly selected = signal<Role | null>(null);
  label = '';
  description = '';
  active = true;
  reason = '';
  permissions: string[] = [];
  constructor() {
    void this.load();
  }
  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [roles, catalog] = await Promise.all([
        this.api.get<Role[]>('/api/v1/access/roles'),
        this.api.get<PermissionDefinition[]>('/api/v1/access/permissions'),
      ]);
      this.roles.set(roles);
      this.catalog.set(catalog);
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }
  open(role: Role | null = null): void {
    this.selected.set(role);
    this.label = role?.label ?? '';
    this.description = role?.description ?? '';
    this.active = role?.isActive ?? true;
    this.permissions = [...(role?.permissions ?? [])];
    this.reason = '';
    this.dialogError.set('');
    this.editor.nativeElement.showModal();
  }
  close(): void {
    if (!this.busy()) this.editor.nativeElement.close();
  }
  cancel(event: Event): void {
    if (this.busy()) event.preventDefault();
  }
  toggle(permission: string, event: Event): void {
    this.permissions = (event.target as HTMLInputElement).checked
      ? [...new Set([...this.permissions, permission])]
      : this.permissions.filter((p) => p !== permission);
  }
  all(): void {
    this.permissions =
      this.permissions.length === this.catalog().length ? [] : this.catalog().map((p) => p.key);
  }
  async save(): Promise<void> {
    if (this.busy()) return;
    const role = this.selected();
    this.dialogError.set('');
    if (
      role?.accounts &&
      !(await this.alerts.confirm({
        title: 'Actualizar el rol',
        text: `El cambio se aplicará a ${role.accounts} ${role.accounts === 1 ? 'cuenta' : 'cuentas'} y cerrará sus sesiones. Si tu cuenta usa este rol, tendrás que ingresar de nuevo.`,
        confirmText: 'Actualizar permisos',
      }))
    )
      return;
    this.busy.set(true);
    try {
      await this.api.post(
        '/api/v1/access/roles' + (role ? '/' + encodeURIComponent(role.key) : ''),
        {
          label: this.label,
          description: this.description,
          isActive: this.active,
          permissions: this.permissions,
          reason: this.reason,
          version: role?.version,
        },
      );
      this.editor.nativeElement.close();
      await this.alerts.success('Rol guardado', 'Los permisos quedaron actualizados.');
      await this.load();
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
