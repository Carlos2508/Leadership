import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccessApi, AuditEntry, PageResult } from '../core/access-api';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';

const actionLabels: Record<string, string> = {
  OwnerProvisioned: 'Gestor inicial creado',
  AccountInvited: 'Cuenta invitada',
  AccessProfileChanged: 'Rol de la cuenta modificado',
  AccessRoleCreated: 'Rol de acceso creado',
  AccessRoleUpdated: 'Rol de acceso modificado',
  AccountRolePermissionsChanged: 'Permisos del rol actualizados',
  AccessRolesMigrated: 'Roles de acceso preparados',
  AccountEnabled: 'Cuenta habilitada',
  AccountSuspended: 'Cuenta suspendida',
  SessionsRevoked: 'Sesiones cerradas',
  AccessCodeIssued: 'Código generado',
  AccessCodesRevoked: 'Códigos anulados',
  AccountActivated: 'Cuenta activada',
  PasswordRecovered: 'Contraseña recuperada',
  LoginSucceeded: 'Ingreso correcto',
  LoginFailed: 'Ingreso rechazado',
  LoginLockedOut: 'Cuenta bloqueada por intentos',
  LoginSecondFactorRequired: 'Contraseña correcta; falta segundo paso',
  LoginSecondFactorFailed: 'Código de verificación rechazado',
  LoginRecoveryCodeUsed: 'Ingreso con código de respaldo',
  Logout: 'Salida de la cuenta',
  MfaEnabled: 'Verificación en dos pasos activada',
  MfaDisabled: 'Verificación en dos pasos desactivada',
  MfaRecoveryCodesRegenerated: 'Códigos de respaldo renovados',
  MfaReset: 'Verificación en dos pasos restablecida',
  PersonCreated: 'Persona registrada',
  PersonUpdated: 'Datos personales actualizados',
  StudentProfileAdded: 'Perfil de estudiante agregado',
  GuardianProfileAdded: 'Perfil de encargado agregado',
  StaffProfileAdded: 'Perfil de personal agregado',
  PersonArchived: 'Persona archivada',
  PersonRestored: 'Persona restaurada',
  GuardianLinked: 'Vínculo familiar registrado',
  GuardianFacultiesChanged: 'Facultades modificadas',
  GuardianUnlinked: 'Vínculo familiar finalizado',
  AccountLinkedToPerson: 'Cuenta vinculada a persona',
  AccountUnlinkedFromPerson: 'Cuenta desvinculada de persona',
  AcademicYearCreated: 'Ciclo creado',
  AcademicYearUpdated: 'Ciclo modificado',
  AcademicYearStatusChanged: 'Estado del ciclo cambiado',
  AcademicYearDefaultChanged: 'Ciclo predeterminado cambiado',
  AcademicPeriodAdded: 'Período agregado',
  AcademicPeriodUpdated: 'Período modificado',
  AcademicPeriodRemoved: 'Período eliminado',
  GradeCreated: 'Grado creado',
  GradeUpdated: 'Grado modificado',
  SectionCreated: 'Sección creada',
  SectionUpdated: 'Sección modificada',
  SectionRemoved: 'Sección eliminada',
  StudentEnrolled: 'Estudiante inscrito',
  EnrollmentPlacementChanged: 'Ubicación cambiada',
  StudentWithdrawn: 'Retiro registrado',
  StudentReenrolled: 'Reingreso registrado',
  SubjectCreated: 'Materia creada',
  SubjectUpdated: 'Materia modificada',
  CoursesCreated: 'Cursos creados',
  TeacherAssigned: 'Docente asignado',
  TeacherUnassigned: 'Asignación docente finalizada',
  ScoresRecorded: 'Notas registradas',
  PublishedScoresCorrected: 'Nota publicada corregida',
  GradesPublished: 'Notas publicadas',
  AssignmentCreated: 'Actividad creada',
  AssignmentUpdated: 'Actividad modificada',
  AssignmentPublished: 'Actividad publicada',
  AssignmentClosed: 'Actividad cerrada',
  AssignmentSubmitted: 'Entrega registrada',
  SubmissionReviewed: 'Entrega revisada',
  SubmissionFileUploaded: 'Archivo adjuntado',
  SubmissionFileRemoved: 'Archivo quitado',
  AttendanceRecorded: 'Asistencia registrada',
  FeeConceptCreated: 'Concepto de cobro creado',
  FeeConceptUpdated: 'Concepto de cobro modificado',
  BillingPeriodAdded: 'Cuota agregada al calendario',
  BillingPeriodUpdated: 'Cuota modificada',
  ClearanceCutoffCreated: 'Corte de solvencia creado',
  ChargesGenerated: 'Cargos emitidos',
  ChargeCreated: 'Cargo emitido',
  ChargeAdjusted: 'Ajuste a cargo',
  ChargeVoided: 'Cargo anulado',
  PaymentRecorded: 'Pago registrado',
  PaymentReversed: 'Pago revertido',
  AnnouncementCreated: 'Aviso redactado',
  AnnouncementUpdated: 'Aviso modificado',
  AnnouncementPublished: 'Aviso publicado',
  AnnouncementArchived: 'Aviso archivado',
  LegacyPeopleImported: 'Importación del legado aplicada',
  StudentCardsIssued: 'Carnets emitidos',
  StudentCardReissued: 'Carnet reemplazado',
  PersonPhotoUpdated: 'Fotografía actualizada',
  PersonPhotoRemoved: 'Fotografía quitada',
  GateManualMark: 'Marca manual en portería',
  GateMarkVoided: 'Marca de portería anulada',
  GateDeviceRegistered: 'Teléfono lector registrado',
  GateDeviceRevoked: 'Teléfono lector revocado',
  ProfilePermissionsExtended: 'Perfil ampliado por actualización',
};
const typeLabels: Record<string, string> = {
  Person: 'Persona',
  Relationship: 'Vínculo',
  AcademicYear: 'Ciclo',
  Grade: 'Grado',
  Course: 'Curso',
  Enrollment: 'Inscripción',
  Subject: 'Materia',
  Assignment: 'Actividad',
  Charge: 'Cargo',
  Payment: 'Pago',
  FeeConcept: 'Concepto',
  Announcement: 'Aviso',
  ImportBatch: 'Lote de importación',
  GateDevice: 'Teléfono lector',
};
const detailLabels: Record<string, string> = {
  basic: 'Cuenta básica',
  'access-manager': 'Gestión de accesos',
  auditor: 'Consulta de auditoría',
  'access-owner': 'Gestión y auditoría',
  'people-reader': 'Consulta de personas',
  registrar: 'Secretaría',
  'academic-coordinator': 'Coordinación académica',
  cashier: 'Caja',
  'finance-manager': 'Administración financiera',
  administrator: 'Administración institucional',
  Activation: 'Activación',
  Recovery: 'Recuperación',
  Draft: 'Borrador',
  EnrollmentOpen: 'Inscripciones abiertas',
  Active: 'En curso',
  Closed: 'Cerrado',
  Student: 'Estudiante',
  Guardian: 'Encargado',
  Section: 'Sección',
  Grade: 'Grado',
  School: 'Todo el colegio',
  Course: 'Curso',
  Mother: 'Madre',
  Father: 'Padre',
  Grandparent: 'Abuela o abuelo',
  Sibling: 'Hermana o hermano',
  OtherRelative: 'Otro familiar',
  LegalGuardian: 'Tutela legal',
  Other: 'Otro vínculo',
  AcademicRecords: 'información académica',
  Finance: 'estado de cuenta',
  BillingResponsible: 'responsable de pago',
  EmergencyContact: 'emergencia',
  Pickup: 'recoger',
  Communications: 'comunicaciones',
  Scholarship: 'Beca',
  Discount: 'Descuento',
  Waiver: 'Exoneración',
  Cash: 'Efectivo',
  Transfer: 'Transferencia',
  Deposit: 'Depósito',
  Card: 'Tarjeta',
  Check: 'Cheque',
  gatekeeper: 'Portería',
  'attendance-supervisor': 'Control de asistencia',
  In: 'Entrada',
  Out: 'Salida',
  first: 'primer carnet',
  replaced: 'reemplazo',
  added: 'nueva',
  clearance: 'cuenta para solvencia',
  'no-clearance': 'no cuenta para solvencia',
};

/** Durable audit trail of every module, filterable by area. Shows identifiers, never personal data of minors. */
@Component({
  imports: [Shell, Icon, FormsModule],
  template: `
    <app-shell title="Auditoría">
      <div class="page-heading">
        <div>
          <span class="eyebrow">CAMBIOS CON TRAZABILIDAD</span>
          <h1>Auditoría</h1>
          <p>
            Quién realizó cada cambio, cuándo y con qué motivo. Los registros no se pueden modificar
            ni borrar.
          </p>
        </div>
        <button class="button outline" (click)="load()" [disabled]="loading()">
          <app-icon name="refresh" />Actualizar
        </button>
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      }
      <section class="card directory-card" [attr.aria-busy]="loading()">
        <div class="filters">
          <label class="select-field"
            ><span>Área</span
            ><select name="area" [(ngModel)]="area" (ngModelChange)="load()">
              <option value="changes">Cambios (sin ingresos de sesión)</option>
              <option value="all">Todo, incluidos ingresos</option>
              <option value="access">Cuentas y accesos</option>
              <option value="people">Personas y familias</option>
              <option value="academic">Académico</option>
              <option value="finance">Finanzas</option>
              <option value="communication">Avisos</option>
              <option value="gate">Carnets y portería</option>
            </select></label
          >
        </div>
        @if (loading() && !data()) {
          <p role="status" class="loading-state">Cargando auditoría…</p>
        } @else if (data(); as page) {
          <p class="directory-summary" aria-live="polite">
            {{ page.total }} {{ page.total === 1 ? 'evento' : 'eventos' }} · Fechas en la zona
            horaria del colegio.
          </p>
          @if (page.items.length) {
            <div class="table-scroll" tabindex="0" role="region" aria-label="Tabla de auditoría">
              <table class="stack-table audit-table">
                <caption class="sr-only">
                  Registro de operaciones
                </caption>
                <thead>
                  <tr>
                    <th scope="col">Operación</th>
                    <th scope="col">Fecha</th>
                    <th scope="col">Realizada por</th>
                    <th scope="col">Registro afectado</th>
                    <th scope="col">Motivo</th>
                  </tr>
                </thead>
                <tbody>
                  @for (event of page.items; track event.id) {
                    <tr>
                      <td>
                        <strong>{{ label(event.action) }}</strong>
                        @if (detail(event.detail); as text) {
                          <small class="audit-detail">{{ text }}</small>
                        }
                      </td>
                      <td data-label="Fecha" class="nowrap">{{ api.date(event.occurredAt) }}</td>
                      <td data-label="Por">
                        {{ event.actor || 'Sistema o intento sin identificar' }}
                      </td>
                      <td data-label="Registro">{{ targetLabel(event) }}</td>
                      <td data-label="Motivo" class="audit-reason">{{ event.reason || '—' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <div class="empty-state">
              <app-icon name="chart" />
              <h2>Sin eventos en esta área</h2>
              <p>Las operaciones aparecerán aquí a medida que se registren.</p>
            </div>
          }
          <div class="pagination">
            <button
              class="button outline"
              [disabled]="page.page <= 1 || loading()"
              (click)="load(page.page - 1)"
            >
              Anterior</button
            ><span>Página {{ page.page }} de {{ pages(page) }}</span
            ><button
              class="button outline"
              [disabled]="page.page * page.pageSize >= page.total || loading()"
              (click)="load(page.page + 1)"
            >
              Siguiente
            </button>
          </div>
        }
      </section>
    </app-shell>
  `,
})
export class SecurityAudit {
  readonly api = inject(AccessApi);
  readonly data = signal<PageResult<AuditEntry> | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  area = 'changes';

  constructor() {
    void this.load();
  }
  async load(page = 1): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      this.data.set(
        await this.api.get<PageResult<AuditEntry>>(`/api/v1/audit?page=${page}&area=${this.area}`),
      );
    } catch (error) {
      this.error.set(this.api.message(error));
    } finally {
      this.loading.set(false);
    }
  }
  pages(page: PageResult<AuditEntry>): number {
    return Math.max(1, Math.ceil(page.total / page.pageSize));
  }
  label(action: string): string {
    return actionLabels[action] ?? 'Operación registrada';
  }
  detail(value: string): string {
    if (!value) return '';
    // Instants inside details (e.g. the time of a voided gate mark) are shown in the school's time zone.
    value = value.replace(
      /\d{4}-\d{2}-\d{2}T[\d:.]+(?:[+-]\d{2}:\d{2}|Z)/g,
      (instant) => '«' + this.api.date(instant) + '»',
    );
    // Details are technical keys (statuses, kinds, faculties, codes, counts); translate known ones, shorten identifiers.
    return value
      .split(/(«[^»]*»|[:,])/)
      .map((token) => {
        if (token.startsWith('«')) return token.slice(1, -1);
        if (token === ':') return ' · ';
        if (token === ',') return ', ';
        if (/^[a-f0-9]{32}$/i.test(token)) return token.slice(0, 8);
        return detailLabels[token] ?? token;
      })
      .join('')
      .slice(0, 120);
  }
  targetLabel(event: AuditEntry): string {
    if (event.targetType)
      return `${typeLabels[event.targetType] ?? 'Registro'} ${(event.targetId ?? '').slice(0, 8)}`;
    return event.target || '—';
  }
}
