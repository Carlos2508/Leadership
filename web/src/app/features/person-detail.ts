import { Component, ElementRef, ViewChild, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AccessApi, PageResult } from '../core/access-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import {
  ContactItem,
  LinkableAccount,
  PeopleCatalog,
  PersonDetail as PersonRecord,
  PersonSummary,
  RelationshipItem,
  accountStatusLabels,
  contactLabels,
  facultyLabels,
  formatDateOnly,
  relationshipLabels,
} from '../core/people-api';
import { EnrollmentHistory, enrollmentStatusLabels } from '../core/academic-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

type DialogMode =
  | 'edit'
  | 'student-role'
  | 'staff-role'
  | 'link'
  | 'relationship'
  | 'end'
  | 'account'
  | 'unlink'
  | 'status';

@Component({
  imports: [Shell, Icon, FormsModule, RouterLink, NgTemplateOutlet],
  templateUrl: './person-detail.html',
})
export class PersonDetail {
  readonly id = input.required<string>();
  readonly api = inject(AccessApi);
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  @ViewChild('detailDialog') private dialog!: ElementRef<HTMLDialogElement>;
  @ViewChild('noticeRef') private noticeRef?: ElementRef<HTMLElement>;

  readonly person = signal<PersonRecord | null>(null);
  readonly enrollments = signal<EnrollmentHistory[]>([]);
  readonly enrollmentLabels = enrollmentStatusLabels;
  readonly catalog = signal<PeopleCatalog | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly notFound = signal(false);
  readonly notice = signal('');
  readonly dialogError = signal('');
  readonly mode = signal<DialogMode>('edit');
  readonly selectedRelationship = signal<RelationshipItem | null>(null);
  readonly candidates = signal<PersonSummary[]>([]);
  readonly accounts = signal<LinkableAccount[]>([]);
  readonly searching = signal(false);
  readonly canManage = computed(() => this.session.can('people.manage'));
  readonly canLink = computed(
    () => this.session.can('people.manage') && this.session.can('accounts.manage'),
  );
  readonly active = computed(() => this.person()?.status === 'Active');

  readonly relationshipLabels = relationshipLabels;
  readonly contactLabels = contactLabels;
  readonly facultyLabels = facultyLabels;
  readonly accountStatusLabels = accountStatusLabels;

  // Dialog state
  edit = {
    givenNames: '',
    familyNames: '',
    preferredName: '',
    birthDate: '',
    contacts: [] as ContactItem[],
  };
  studentCode = '';
  position = '';
  reason = '';
  kind = 'Mother';
  startsOn = '';
  faculties: Record<string, boolean> = {};
  linkSide: 'guardian' | 'student' = 'guardian';
  candidateSearch = '';
  candidateId = '';
  accountSearch = '';
  accountId = '';

  constructor() {
    queueMicrotask(() => void this.load());
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [person, catalog] = await Promise.all([
        this.api.get<PersonRecord>(`/api/v1/people/${encodeURIComponent(this.id())}`),
        this.catalog() ?? this.api.get<PeopleCatalog>('/api/v1/people/catalog'),
      ]);
      this.person.set(person);
      this.catalog.set(catalog);
      this.enrollments.set(
        person.studentCode && this.session.can('academic.read')
          ? await this.api.get<EnrollmentHistory[]>(`/api/v1/people/${person.id}/enrollments`)
          : [],
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400))
        this.notFound.set(true);
      else this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }

  date(value: string | null): string {
    return formatDateOnly(value, this.school.locale.culture);
  }

  activeRelationships(items: RelationshipItem[]): RelationshipItem[] {
    return items.filter((x) => !x.endedAt);
  }
  endedRelationships(items: RelationshipItem[]): RelationshipItem[] {
    return items.filter((x) => !!x.endedAt);
  }

  private open(mode: DialogMode): void {
    this.mode.set(mode);
    this.dialogError.set('');
    this.reason = '';
    this.dialog.nativeElement.showModal();
  }

  openEdit(): void {
    const p = this.person();
    if (!p) return;
    this.edit = {
      givenNames: p.givenNames,
      familyNames: p.familyNames,
      preferredName: p.preferredName ?? '',
      birthDate: p.birthDate ?? '',
      contacts: p.contacts.map((x) => ({ ...x })),
    };
    this.open('edit');
  }
  addContact(kind: ContactItem['kind']): void {
    this.edit.contacts.push({
      kind,
      value: '',
      label: '',
      isPrimary: !this.edit.contacts.some((x) => x.kind === kind),
    });
  }
  removeContact(index: number): void {
    this.edit.contacts.splice(index, 1);
  }

  openStudentRole(): void {
    this.studentCode = '';
    this.open('student-role');
  }

  openStaffRole(): void {
    this.position = '';
    this.open('staff-role');
  }

  openLink(side: 'guardian' | 'student'): void {
    this.linkSide = side;
    this.kind = 'Mother';
    this.startsOn = '';
    this.faculties = {};
    this.candidateSearch = '';
    this.candidateId = '';
    this.candidates.set([]);
    this.open('link');
  }

  openRelationship(item: RelationshipItem): void {
    this.selectedRelationship.set(item);
    this.kind = item.kind;
    this.faculties = Object.fromEntries(item.faculties.map((x) => [x, true]));
    this.open('relationship');
  }

  openEnd(item: RelationshipItem): void {
    this.selectedRelationship.set(item);
    this.open('end');
  }

  openAccount(): void {
    this.accountSearch = '';
    this.accountId = '';
    this.accounts.set([]);
    this.open('account');
    void this.searchAccounts();
  }

  openUnlink(): void {
    this.open('unlink');
  }

  openStatus(): void {
    this.open('status');
  }

  closeDialog(): void {
    if (this.busy()) return;
    this.dialog.nativeElement.close();
    this.selectedRelationship.set(null);
  }
  onCancel(event: Event): void {
    if (this.busy()) event.preventDefault();
    else this.closeDialog();
  }

  async searchCandidates(): Promise<void> {
    this.searching.set(true);
    try {
      const query = new URLSearchParams({
        search: this.candidateSearch,
        role: this.linkSide,
        status: 'Active',
        page: '1',
      });
      const page = await this.api.get<PageResult<PersonSummary>>(`/api/v1/people?${query}`);
      this.candidates.set(page.items.filter((x) => x.id !== this.id()));
      if (!this.candidates().some((x) => x.id === this.candidateId)) this.candidateId = '';
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.searching.set(false);
    }
  }

  async searchAccounts(): Promise<void> {
    this.searching.set(true);
    try {
      this.accounts.set(
        await this.api.get<LinkableAccount[]>(
          `/api/v1/people/linkable-accounts?search=${encodeURIComponent(this.accountSearch)}`,
        ),
      );
      if (!this.accounts().some((x) => x.id === this.accountId)) this.accountId = '';
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.searching.set(false);
    }
  }

  selectedFaculties(): string[] {
    return (this.catalog()?.faculties ?? []).map((x) => x.key).filter((x) => this.faculties[x]);
  }

  dialogTitle(): string {
    const titles: Record<DialogMode, string> = {
      edit: 'Editar datos personales',
      'student-role': 'Agregar perfil de estudiante',
      'staff-role': 'Agregar perfil de personal',
      link:
        this.linkSide === 'guardian'
          ? 'Vincular un encargado'
          : 'Vincular un estudiante a su cargo',
      relationship: 'Parentesco y facultades',
      end: 'Finalizar vínculo',
      account: 'Vincular cuenta de acceso',
      unlink: 'Desvincular cuenta de acceso',
      status: this.person()?.status === 'Active' ? 'Archivar persona' : 'Restaurar persona',
    };
    return titles[this.mode()];
  }

  async submit(): Promise<void> {
    const p = this.person();
    if (!p || this.busy()) return;
    this.busy.set(true);
    this.dialogError.set('');
    const root = `/api/v1/people/${p.id}`;
    try {
      let message = 'El cambio se guardó y quedó registrado.';
      switch (this.mode()) {
        case 'edit':
          await this.api.post(root, {
            version: p.version,
            givenNames: this.edit.givenNames,
            familyNames: this.edit.familyNames,
            preferredName: this.edit.preferredName || null,
            birthDate: this.edit.birthDate || null,
            contacts: this.edit.contacts.filter((x) => x.value.trim()),
          });
          message = 'Los datos personales se actualizaron.';
          break;
        case 'student-role':
          await this.api.post(root + '/roles', {
            version: p.version,
            role: 'student',
            studentCode: this.studentCode,
          });
          message = 'Se agregó el perfil de estudiante.';
          break;
        case 'staff-role':
          await this.api.post(root + '/roles', { version: p.version, role: 'staff', position: this.position });
          message = 'Se agregó el perfil de personal.';
          break;
        case 'link': {
          if (!this.candidateId) {
            this.dialogError.set(
              this.linkSide === 'guardian'
                ? 'Busca y selecciona al encargado.'
                : 'Busca y selecciona al estudiante.',
            );
            return;
          }
          await this.api.post('/api/v1/relationships', {
            studentId: this.linkSide === 'guardian' ? p.id : this.candidateId,
            guardianId: this.linkSide === 'guardian' ? this.candidateId : p.id,
            kind: this.kind,
            startsOn: this.startsOn || null,
            faculties: this.selectedFaculties(),
            reason: this.reason,
          });
          message = 'El vínculo quedó registrado.';
          break;
        }
        case 'relationship': {
          const item = this.selectedRelationship();
          if (!item) return;
          await this.api.post(`/api/v1/relationships/${item.id}`, {
            version: item.version,
            kind: this.kind,
            faculties: this.selectedFaculties(),
            reason: this.reason,
          });
          message = 'Las facultades se actualizaron.';
          break;
        }
        case 'end': {
          const item = this.selectedRelationship();
          if (!item) return;
          await this.api.post(`/api/v1/relationships/${item.id}/end`, {
            version: item.version,
            reason: this.reason,
          });
          message = 'El vínculo finalizó. Su historial se conserva.';
          break;
        }
        case 'account':
          if (!this.accountId) {
            this.dialogError.set('Busca y selecciona la cuenta que corresponde a esta persona.');
            return;
          }
          await this.api.post(root + '/account', {
            version: p.version,
            userId: this.accountId,
            reason: this.reason,
          });
          message = 'La cuenta quedó vinculada a esta persona.';
          break;
        case 'unlink':
          await this.api.post(root + '/account/unlink', { version: p.version, reason: this.reason });
          message = 'La cuenta se desvinculó. La cuenta sigue existiendo.';
          break;
        case 'status':
          await this.api.post(root + '/status', {
            version: p.version,
            status: p.status === 'Active' ? 'Archived' : 'Active',
            reason: this.reason,
          });
          message = p.status === 'Active' ? 'La persona se archivó.' : 'La persona se restauró.';
          break;
      }
      this.busy.set(false);
      this.closeDialog();
      this.notice.set(message);
      await this.load();
      this.focusNotice();
    } catch (error) {
      this.dialogError.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }

  /** The control that opened a dialog may disappear after the change; keep focus on the confirmation. */
  private focusNotice(): void {
    requestAnimationFrame(() => this.noticeRef?.nativeElement.focus());
  }

  async addGuardianRole(): Promise<void> {
    const p = this.person();
    if (!p || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.post(`/api/v1/people/${p.id}/roles`, { version: p.version, role: 'guardian' });
      this.notice.set('Se agregó el perfil de encargado.');
      await this.load();
      this.focusNotice();
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.busy.set(false);
    }
  }
}
