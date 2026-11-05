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
@Component({
  imports: [Shell, Icon, RouterLink, CyclePipe],
  template: `
    <app-shell title="Mi espacio">
      <div class="page-heading">
        <div>
          <span class="eyebrow">MI COMUNIDAD EDUCATIVA</span>
          <h1>Te damos la bienvenida{{ greetingName() ? ', ' + greetingName() : '' }}.</h1>
          <p>Tu punto de encuentro con {{ school.displayName }}.</p>
        </div>
        <form class="header-logout" action="/Identity/Account/Logout" method="post">
          <input type="hidden" name="__RequestVerificationToken" [value]="session.token()" /><button
            class="button outline"
            type="submit"
            [disabled]="!session.token()"
          >
            <app-icon name="logout" />Cerrar sesión
          </button>
        </form>
      </div>
      @if (session.state() === 'ready') {
        @if (session.mfaPending().length) {
          <div class="feedback action-explanation mfa-banner" role="note">
            <app-icon name="shield" /><span
              >Tu perfil incluye funciones de administración que requieren verificación en dos pasos.
              <a routerLink="/workspace/security">Actívala ahora</a> para usarlas.</span
            >
          </div>
        }
        @if (shortcuts().length) {
          <section class="card quick-card" aria-labelledby="quick-title">
            <div class="card-heading">
              <div>
                <span class="eyebrow">ACCESOS RÁPIDOS</span>
                <h2 id="quick-title">¿Qué necesitas hacer hoy?</h2>
              </div>
            </div>
            <div class="quick-grid">
              @for (item of shortcuts(); track item.link) {
                <a class="quick-tile" [routerLink]="item.link">
                  <span class="icon-tile" [class]="item.tone"><app-icon [name]="item.icon" /></span>
                  <span class="person-copy"><strong>{{ item.label }}</strong><small>{{ item.description }}</small></span>
                </a>
              }
            </div>
          </section>
        } @else if (profile() && !profile()?.person && !myCourses().length) {
          <section class="workspace-welcome card">
            <div class="icon-tile mint"><app-icon name="shield" /></div>
            <h2>Tu cuenta está activa</h2>
            <p>
              Todavía no tiene servicios asignados. Si eres estudiante, encargado o parte del personal,
              comunícate con la administración para vincular tu cuenta.
            </p>
            <div class="connection-status"><span></span>Cuenta conectada a {{ school.displayName }}</div>
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
                    ><strong>{{ item.title }}</strong><small>{{ item.author }} · {{ api.date(item.publishedAt) }}</small></span
                  >
                </li>
              }
            </ul>
            <a class="text-button" routerLink="/workspace/announcements">Ver todos los avisos</a>
          </section>
        }
        @if (profile(); as me) {
          @if (me.person; as person) {
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
                <a class="text-button" [routerLink]="['/workspace/my/students', person.id]">Ver mi ficha</a>
              }
              @if (person.isGuardian) {
                <h3 class="subheading">{{ school.terms.studentPlural }} a tu cargo</h3>
                @for (student of me.students; track student.id) {
                  <article class="relationship-row">
                    <div class="relationship-main">
                      <a [routerLink]="['/workspace/my/students', student.id]">{{ student.displayName }}</a>
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
              <p class="muted-copy">
                Abre cada ficha para ver calificaciones publicadas, tareas y estado de cuenta según
                lo que el colegio haya habilitado.
              </p>
            </section>
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
              <ul class="payment-list">
                @for (course of myCourses(); track course.id) {
                  <li>
                    <span class="person-copy"
                      ><strong>{{ course.subject }}</strong
                      ><small
                        >{{ course.grade }} · {{ course.section ? 'Sección ' + course.section : 'Todo el grado' }} · {{ course.students }}
                        estudiantes</small
                      ></span
                    >
                    <a class="text-button" [routerLink]="['/workspace/courses', course.id]"
                      >Ver lista <span class="sr-only">de {{ course.subject }}</span></a
                    >
                    <a class="text-button" [routerLink]="['/workspace/courses', course.id, 'attendance']"
                      >Asistencia <span class="sr-only">de {{ course.subject }}</span></a
                    >
                    <a class="text-button" [routerLink]="['/workspace/courses', course.id, 'assignments']"
                      >Actividades <span class="sr-only">de {{ course.subject }}</span></a
                    >
                    <a class="text-button" [routerLink]="['/workspace/courses', course.id, 'grades']"
                      >Libreta <span class="sr-only">de {{ course.subject }}</span></a
                    >
                  </li>
                }
              </ul>
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
  readonly greetingName = computed(() => this.profile()?.person?.displayName.split(' ')[0] ?? this.session.username());
  readonly shortcuts = computed(() =>
    [
      { permission: 'people.read', link: '/workspace/people', icon: 'people', tone: 'mint', label: 'Personas y familias', description: 'Fichas, vínculos y facultades' },
      { permission: 'academic.read', link: '/workspace/enrollment', icon: 'school', tone: 'blue', label: 'Inscripciones', description: 'Matrícula por ciclo y ubicación' },
      { permission: 'academic.read', link: '/workspace/courses', icon: 'book', tone: 'purple', label: 'Cursos y docentes', description: 'Materias, listas y asignaciones' },
      { permission: 'academic.read', link: '/workspace/grades', icon: 'star', tone: 'gold', label: 'Publicación de notas', description: 'Avance y publicación por período' },
      { permission: 'finance.read', link: '/workspace/finance', icon: 'wallet', tone: 'mint', label: 'Caja y finanzas', description: 'Cobros, recibos y solvencia' },
      { permission: 'accounts.manage', link: '/workspace/access', icon: 'shield', tone: 'blue', label: 'Administrar cuentas', description: 'Invitaciones, perfiles y accesos' },
      { permission: 'audit.read', link: '/workspace/audit', icon: 'chart', tone: 'purple', label: 'Consultar auditoría', description: 'Registro de cambios sensibles' },
    ].filter((x) => this.session.can(x.permission)),
  );
  readonly facultyLabels = facultyLabels;
  readonly relationshipLabels = relationshipLabels;
  constructor() {
    void this.loadProfile();
  }
  async loadProfile(): Promise<void> {
    if (this.session.state() !== 'ready') return;
    try {
      void this.api.get<FeedItem[]>('/api/v1/me/announcements').then((x) => this.feed.set(x)).catch(() => undefined);
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
