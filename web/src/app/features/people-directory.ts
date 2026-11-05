import { Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi, PageResult } from '../core/access-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { PersonSummary } from '../core/people-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink],
  templateUrl: './people-directory.html',
})
export class PeopleDirectory {
  readonly api = inject(AccessApi);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  private readonly router = inject(Router);
  @ViewChild('personDialog') private dialog!: ElementRef<HTMLDialogElement>;
  readonly data = signal<PageResult<PersonSummary> | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly dialogError = signal('');
  readonly duplicateWarning = signal(false);
  search = '';
  role = 'all';
  status = 'Active';
  form = this.emptyForm();

  constructor() {
    void this.load();
  }

  emptyForm() {
    return {
      givenNames: '',
      familyNames: '',
      preferredName: '',
      birthDate: '',
      phone: '',
      email: '',
      address: '',
      isStudent: false,
      studentCode: '',
      isGuardian: false,
    };
  }

  async load(page = 1): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const query = new URLSearchParams({
        search: this.search,
        role: this.role,
        status: this.status,
        page: String(page),
      });
      this.data.set(await this.api.get<PageResult<PersonSummary>>(`/api/v1/people?${query}`));
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }

  clearFilters(): void {
    this.search = '';
    this.role = 'all';
    this.status = 'Active';
    void this.load();
  }

  openCreate(): void {
    this.form = this.emptyForm();
    this.dialogError.set('');
    this.duplicateWarning.set(false);
    this.dialog.nativeElement.showModal();
  }

  closeDialog(): void {
    if (this.busy()) return;
    this.dialog.nativeElement.close();
  }

  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
    else this.closeDialog();
  }

  async create(confirmDuplicate = false): Promise<void> {
    if (this.busy()) return;
    if (!this.form.isStudent && !this.form.isGuardian) {
      this.dialogError.set('Selecciona al menos un perfil: estudiante o encargado.');
      return;
    }
    this.busy.set(true);
    this.dialogError.set('');
    try {
      const f = this.form;
      const contacts = [
        { kind: 'Phone', value: f.phone, label: '', isPrimary: true },
        { kind: 'Email', value: f.email, label: '', isPrimary: true },
        { kind: 'Address', value: f.address, label: '', isPrimary: true },
      ].filter((x) => x.value.trim());
      const created = await this.api.post<{ id: string }>('/api/v1/people', {
        givenNames: f.givenNames,
        familyNames: f.familyNames,
        preferredName: f.preferredName || null,
        birthDate: f.birthDate || null,
        contacts,
        studentCode: f.isStudent ? f.studentCode : null,
        isGuardian: f.isGuardian,
        confirmDuplicate,
      });
      this.busy.set(false);
      this.dialog.nativeElement.close();
      await this.router.navigate(['/workspace/people', created.id]);
    } catch (error) {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 409 &&
        error.error?.code === 'possible-duplicate'
      )
        this.duplicateWarning.set(true);
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
