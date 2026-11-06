import { Component, computed, inject, signal } from '@angular/core';
import { AccessApi } from '../core/access-api';
import { FeedItem } from './announcements';
import { MyProfile, facultyLabels, relationshipLabels } from '../core/people-api';
import { SessionService } from '../core/session';
import { InstitutionService } from '../core/institution';
import { Shell } from '../shared/shell';
import { Icon } from '../shared/icon';
import { CyclePipe } from '../shared/cycle.pipe';
import { RouterLink } from '@angular/router';
import { money } from '../core/finance-api';

interface Dashboard {
  academic: {
    year: string;
    enrolled: number;
    nextPeriod: string | null;
    recorded: number;
    expected: number;
  } | null;
  finance: {
    pending: number;
    studentsWithBalance: number;
    paymentsToday: number;
    collectedToday: number;
    currency: string;
  } | null;
  gate: { expected: number; arrived: number; inside: number; left: number } | null;
  people: number | null;
}
@Component({
  imports: [Shell, Icon, RouterLink, CyclePipe],
  template: `
    <app-shell title="Mi espacio">
      <div class="page-heading">
        <div>
          <span class="eyebrow">{{ today }}</span>
          <h1>{{ greeting }}{{ greetingName() ? ', ' + greetingName() : '' }}.</h1>
          <p>Aquí tienes lo que necesitas para el día de hoy.</p>
        </div>
      </div>
      @if (session.state() === 'ready') {
        @if (session.mfaPending().length) {
          <div class="feedback action-explanation mfa-banner" role="note">
            <app-icon name="shield" /><span
              >Tu perfil incluye funciones de administración que requieren verificación en dos
              pasos. <a routerLink="/workspace/security">Actívala ahora</a> para usarlas.</span
            >
          </div>
        }
        @if (stats(); as st) {
          @if (st.academic || st.finance || st.gate || st.people !== null) {
            <section class="kpi-grid" aria-label="Hoy en el colegio">
              @if (st.gate; as g) {
                <a class="kpi-card" routerLink="/workspace/gate/report">
                  <span class="icon-tile mint"><app-icon name="clock" /></span>
                  <span class="kpi-copy">
                    <small>Entradas de hoy</small>
                    <strong
                      >{{ g.arrived }} <span>de {{ g.expected }}</span></strong
                    >
                    <em
                      >{{ g.inside }} dentro · {{ g.left }}
                      {{ g.left === 1 ? 'salió' : 'salieron' }}</em
                    >
                  </span>
                  <span class="kpi-bar" aria-hidden="true"
                    ><span [style.width.%]="g.expected ? (g.arrived / g.expected) * 100 : 0"></span
                  ></span>
                </a>
              }
              @if (st.academic; as a) {
                <a class="kpi-card" routerLink="/workspace/enrollment">
                  <span class="icon-tile blue"><app-icon name="school" /></span>
                  <span class="kpi-copy">
                    <small>Inscritos</small>
                    <strong>{{ a.enrolled }}</strong>
                    <em
                      >{{ a.enrolled === 1 ? 'estudiante activo' : 'estudiantes activos' }} ·
                      {{ a.year | cycle }}</em
                    >
                  </span>
                </a>
                @if (a.nextPeriod) {
                  <a class="kpi-card" routerLink="/workspace/grades">
                    <span class="icon-tile gold"><app-icon name="star" /></span>
                    <span class="kpi-copy">
                      <small>Notas de {{ a.nextPeriod }}</small>
                      <strong
                        >{{ a.recorded }} <span>de {{ a.expected }}</span></strong
                      >
                      <em>{{
                        a.expected && a.recorded >= a.expected
                          ? 'Lista para publicar'
                          : 'registradas por docentes'
                      }}</em>
                    </span>
                    <span class="kpi-bar gold" aria-hidden="true"
                      ><span
                        [style.width.%]="a.expected ? (a.recorded / a.expected) * 100 : 0"
                      ></span
                    ></span>
                  </a>
                }
              }
              @if (st.finance; as f) {
                <a class="kpi-card" routerLink="/workspace/finance/clearance">
                  <span class="icon-tile" [class.red]="f.pending > 0" [class.mint]="f.pending <= 0"
                    ><app-icon name="wallet"
                  /></span>
                  <span class="kpi-copy">
                    <small>Saldo pendiente</small>
                    <strong>{{ money(f.pending, f.currency) }}</strong>
                    <em
                      >{{ f.studentsWithBalance }}
                      {{ f.studentsWithBalance === 1 ? 'estudiante' : 'estudiantes' }} · hoy
                      {{ f.paymentsToday }} {{ f.paymentsToday === 1 ? 'pago' : 'pagos' }} ({{
                        money(f.collectedToday, f.currency)
                      }})</em
                    >
                  </span>
                </a>
              }
            </section>
          }
        }
        @if (shortcuts().length) {
          <section class="card quick-card" aria-labelledby="quick-title">
            <div class="card-heading">
              <div>
                <span class="eyebrow">ACCESOS RÁPIDOS</span>
                <h2 id="quick-title">Tus módulos</h2>
              </div>
            </div>
            <div class="quick-grid">
              @for (item of shortcuts(); track item.link) {
                <a class="quick-tile" [routerLink]="item.link">
                  <span class="icon-tile" [class]="item.tone"><app-icon [name]="item.icon" /></span>
                  <span class="person-copy"
                    ><strong>{{ item.label }}</strong
                    ><small>{{ item.description }}</small></span
                  >
                </a>
              }
            </div>
          </section>
        } @else if (profile() && !profile()?.person && !myCourses().length) {
          <section class="workspace-welcome card">
            <div class="icon-tile mint"><app-icon name="shield" /></div>
            <h2>Tu cuenta está activa</h2>
            <p>
              Todavía no tiene servicios asignados. Si eres estudiante, encargado o parte del
              personal, comunícate con la administración para vincular tu cuenta.
            </p>
            <div class="connection-status">
              <span></span>Cuenta conectada a {{ school.displayName }}
            </div>
          </section>
        }
        @if (feed().length) {
          <section class="card my-profile" aria-labelledby="feed-title">
            <div class="card-heading">
              <div>
                <span class="eyebrow">AVISOS</span>
                <h2 id="feed-title">
                  Avisos recientes
                  @if (unread()) {
                    <span class="pill gold">{{ unread() }} sin leer</span>
                  }
                </h2>
              </div>
              <app-icon name="mail" />
            </div>
            <ul class="payment-list">
              @for (item of feed().slice(0, 4); track item.id) {
                <li>
                  <span class="unread-dot" [class.hidden-dot]="item.read" aria-hidden="true"></span>
                  <span class="person-copy"
                    ><strong>{{ item.title }}</strong
                    ><small>{{ item.author }} · {{ api.date(item.publishedAt) }}</small></span
                  >
                </li>
              }
            </ul>
            <a class="text-button" routerLink="/workspace/announcements">Ver todos los avisos</a>
          </section>
        }
        @if (profile(); as me) {
          @if (me.person; as person) {
            <!-- Staff-only accounts: their name is in the greeting and their work in «Mis cursos»; no empty profile card. -->
            @if (person.studentCode || person.isGuardian) {
              <section class="card my-profile" aria-labelledby="my-profile-title">
                <div class="card-heading">
                  <div>
                    <span class="eyebrow">TU FICHA</span>
                    <h2 id="my-profile-title">{{ person.displayName }}</h2>
                  </div>
                  <app-icon name="school" />
                </div>
                <p class="role-list">
                  @if (person.studentCode) {
                    <span class="pill blue">Estudiante · {{ person.studentCode }}</span>
                  }
                  @if (person.isGuardian) {
                    <span class="pill purple">Encargado</span>
                  }
                </p>
                @if (me.placement; as placement) {
                  <p class="placement-line">
                    {{ placement.year | cycle }} · {{ placement.grade }}
                    {{ placement.section ? '· Sección ' + placement.section : '' }}
                  </p>
                }
                @if (person.studentCode) {
                  <a
                    class="button compact-button profile-cta"
                    [routerLink]="['/workspace/my/students', person.id]"
                    ><app-icon name="book" />Ver mis notas, tareas y asistencia</a
                  >
                }
                @if (person.isGuardian) {
                  <h3 class="subheading">{{ school.terms.studentPlural }} a tu cargo</h3>
                  @for (student of me.students; track student.id) {
                    <article class="relationship-row">
                      <div class="relationship-main">
                        <a [routerLink]="['/workspace/my/students', student.id]">{{
                          student.displayName
                        }}</a>
                        <span class="muted-copy"
                          >{{ relationshipLabels[student.relationship] }} ·
                          {{ student.studentCode }}</span
                        >
                        @if (student.placement; as placement) {
                          <span class="placement-line"
                            >{{ placement.year | cycle }} · {{ placement.grade }}
                            {{ placement.section ? '· Sección ' + placement.section : '' }}</span
                          >
                        }
                        <div class="faculty-chips">
                          @for (faculty of student.faculties; track faculty) {
                            <span class="chip">{{ facultyLabels[faculty] }}</span>
                          } @empty {
                            <span class="chip empty-chip">Sin facultades asignadas</span>
                          }
                        </div>
                      </div>
                    </article>
                  } @empty {
                    <p class="muted-copy">
                      Aún no hay estudiantes vinculados a tu ficha. Comunícate con la administración
                      si esperabas verlos aquí.
                    </p>
                  }
                }
                @if (person.isGuardian && me.students.length) {
                  <p class="muted-copy">
                    Abre cada ficha para ver calificaciones publicadas, tareas y estado de cuenta
                    según lo que el colegio haya habilitado.
                  </p>
                }
              </section>
            }
          }
          @if (myCourses().length) {
            <section class="card my-profile" aria-labelledby="my-courses-title">
              <div class="card-heading">
                <div>
                  <span class="eyebrow">DOCENCIA</span>
                  <h2 id="my-courses-title">Mis cursos</h2>
                </div>
                <app-icon name="book" />
              </div>
              <ul class="payment-list course-rows">
                @for (course of myCourses(); track course.id) {
                  <li>
                    <span class="person-copy"
                      ><strong>{{ course.subject }}</strong
                      ><small
                        >{{ course.grade }} ·
                        {{ course.section ? 'Sección ' + course.section : 'Todo el grado' }} ·
                        {{ course.students }} estudiantes</small
                      ></span
                    >
                    <span class="course-actions">
                      <a
                        class="button outline compact-button"
                        [routerLink]="['/workspace/courses', course.id]"
                        ><app-icon name="people" />Ver lista
                        <span class="sr-only">de {{ course.subject }}</span></a
                      >
                      <a
                        class="button outline compact-button"
                        [routerLink]="['/workspace/courses', course.id, 'attendance']"
                        ><app-icon name="check" />Asistencia
                        <span class="sr-only">de {{ course.subject }}</span></a
                      >
                      <a
                        class="button outline compact-button"
                        [routerLink]="['/workspace/courses', course.id, 'assignments']"
                        ><app-icon name="book" />Actividades
                        <span class="sr-only">de {{ course.subject }}</span></a
                      >
                      <a
                        class="button compact-button"
                        [routerLink]="['/workspace/courses', course.id, 'grades']"
                        ><app-icon name="star" />Libreta
                        <span class="sr-only">de {{ course.subject }}</span></a
                      >
                    </span>
                  </li>
                }
              </ul>
            </section>
          }
          @if (me.person?.isStaff && !myCourses().length && !shortcuts().length) {
            <section class="workspace-welcome card">
              <div class="icon-tile purple"><app-icon name="book" /></div>
              <h2>Aún no tienes cursos asignados</h2>
              <p>
                Cuando coordinación académica te asigne un curso, aquí verás su lista, asistencia,
                actividades y libreta.
              </p>
            </section>
          }
        } @else if (profileError()) {
          <p class="feedback error-feedback" role="alert">{{ profileError() }}</p>
        }
        <section class="card contact-card">
          <span class="eyebrow">ESTAMOS PARA AYUDARTE</span>
          <h2>{{ school.displayName }}</h2>
          <p>Para consultas sobre tu cuenta, comunícate con la administración.</p>
          @if (school.contact.phone) {
            <p>{{ school.contact.phone }}</p>
          }
          @if (school.contact.email) {
            <p>{{ school.contact.email }}</p>
          }
          @if (school.contact.address) {
            <p>{{ school.contact.address }}</p>
          }
        </section>
      } @else {
        <section class="card empty-state" role="alert">
          <app-icon name="shield" />
          <h2>
            {{
              session.state() === 'denied'
                ? 'Tu cuenta necesita autorización'
                : 'No pudimos cargar tu espacio'
            }}
          </h2>
          <p>
            {{
              session.state() === 'denied'
                ? 'Comunícate con la administración para revisar tu acceso a este colegio.'
                : 'Revisa tu conexión e inténtalo de nuevo.'
            }}
          </p>
          <button class="button" (click)="retry()">Volver a intentar</button>
        </section>
      }
    </app-shell>
  `,
})
export class Workspace {
  readonly session = inject(SessionService);
  readonly school = inject(InstitutionService).current;
  readonly api = inject(AccessApi);
  readonly feed = signal<FeedItem[]>([]);
  readonly unread = computed(() => this.feed().filter((x) => !x.read).length);
  readonly profile = signal<MyProfile | null>(null);
  readonly profileError = signal('');
  readonly myCourses = signal<
    { id: string; subject: string; grade: string; section: string | null; students: number }[]
  >([]);
  readonly greetingName = computed(
    () => this.profile()?.person?.displayName.split(' ')[0] ?? this.session.username(),
  );
  readonly shortcuts = computed(() =>
    [
      {
        permission: 'people.read',
        link: '/workspace/students',
        icon: 'people',
        tone: 'mint',
        label: 'Estudiantes',
        description: 'Fichas, familias y carnets',
      },
      {
        permission: 'people.read',
        link: '/workspace/parents',
        icon: 'users',
        tone: 'mint',
        label: 'Padres',
        description: 'Contactos y estudiantes a cargo',
      },
      {
        permission: 'people.read',
        link: '/workspace/teachers',
        icon: 'school',
        tone: 'mint',
        label: 'Docentes',
        description: 'Expedientes del equipo',
      },
      {
        permission: 'academic.read',
        link: '/workspace/enrollment',
        icon: 'school',
        tone: 'blue',
        label: 'Inscripciones',
        description: 'Matrícula por ciclo y ubicación',
      },
      {
        permission: 'academic.read',
        link: '/workspace/courses',
        icon: 'book',
        tone: 'purple',
        label: 'Cursos',
        description: 'Materias, listas y asignaciones',
      },
      {
        permission: 'academic.read',
        link: '/workspace/grades',
        icon: 'star',
        tone: 'gold',
        label: 'Publicación de notas',
        description: 'Avance y publicación por período',
      },
      {
        permission: 'finance.read',
        link: '/workspace/finance',
        icon: 'wallet',
        tone: 'mint',
        label: 'Caja y cobros',
        description: 'Cobros, recibos y solvencia',
      },
      {
        permission: 'attendance.gate',
        link: '/workspace/gate',
        icon: 'scan',
        tone: 'mint',
        label: 'Lector de entrada',
        description: 'Marca entradas y salidas con carnet',
      },
      {
        permission: 'attendance.read',
        link: '/workspace/gate/report',
        icon: 'clock',
        tone: 'blue',
        label: 'Entradas y salidas',
        description: 'Reporte del día e historial',
      },
      {
        permission: 'accounts.manage',
        link: '/workspace/access',
        icon: 'key',
        tone: 'gold',
        label: 'Administrar cuentas',
        description: 'Invitaciones, perfiles y accesos',
      },
      {
        permission: 'audit.read',
        link: '/workspace/audit',
        icon: 'chart',
        tone: 'purple',
        label: 'Consultar auditoría',
        description: 'Registro de cambios sensibles',
      },
    ].filter((x) => this.session.can(x.permission)),
  );
  readonly facultyLabels = facultyLabels;
  readonly relationshipLabels = relationshipLabels;
  readonly stats = signal<Dashboard | null>(null);
  /** Greeting and long date in the school's time zone (e.g. "Buenas tardes" · "jueves, 5 de noviembre"). */
  readonly greeting = (() => {
    const hour = Number(
      new Intl.DateTimeFormat('en-US', {
        hour: 'numeric',
        hourCycle: 'h23',
        timeZone: this.school.locale.timeZone,
      }).format(new Date()),
    );
    return hour < 12 ? 'Buenos días' : hour < 19 ? 'Buenas tardes' : 'Buenas noches';
  })();
  readonly today = new Intl.DateTimeFormat(this.school.locale.culture, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    timeZone: this.school.locale.timeZone,
  })
    .format(new Date())
    .toUpperCase();
  constructor() {
    void this.loadProfile();
  }
  money(value: number, currency: string): string {
    return money(value, currency, this.school.locale.culture);
  }
  async loadProfile(): Promise<void> {
    if (this.session.state() !== 'ready') return;
    try {
      void this.api
        .get<FeedItem[]>('/api/v1/me/announcements')
        .then((x) => this.feed.set(x))
        .catch(() => undefined);
      // Figures only appear for staff with read permissions; the server returns null sections otherwise.
      if (this.session.permissions().length)
        void this.api
          .get<Dashboard>('/api/v1/dashboard')
          .then((x) => this.stats.set(x))
          .catch(() => undefined);
      const profile = await this.api.get<MyProfile>('/api/v1/me');
      this.profile.set(profile);
      if (profile.person?.isStaff) this.myCourses.set(await this.api.get('/api/v1/me/courses'));
    } catch (error) {
      this.profileError.set(this.api.message(error));
    }
  }
  retry(): void {
    window.location.reload();
  }
}
