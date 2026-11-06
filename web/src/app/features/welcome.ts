import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UpperCasePipe } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { InstitutionService } from '../core/institution';
import { Icon } from '../shared/icon';

/**
 * Public home page of the school: presentation, what families do in "Mi espacio", contact and location.
 * Every institutional text, number and address comes from config/institution.json; empty sections are hidden.
 * The Google map loads only when the visitor asks for it (no third-party request on page load).
 */
@Component({
  imports: [RouterLink, Icon, UpperCasePipe],
  template: `
    <a class="skip-link" href="#content">Saltar al contenido</a>
    <div class="public-site">
      <header class="public-header">
        <div class="public-header-inner">
          <a class="brand public-brand" routerLink="/"
            ><img [src]="school.assets.mainLogo" [alt]="school.displayName" /><span
              aria-hidden="true"
              >{{ school.shortName }}<small>{{ school.campusLabel }}</small></span
            ></a
          >
          <nav class="public-nav" aria-label="Secciones">
            @if (hasAbout()) {
              <a href="#nosotros">Nosotros</a>
            }
            @if (school.public.programs.length) {
              <a href="#niveles">Niveles</a>
            }
            <a href="#plataforma">Mi espacio</a>
            <a href="#contacto">Contacto</a>
          </nav>
          <a
            href="/Identity/Account/Login"
            class="button public-cta"
            aria-label="Ingresar a mi espacio"
            ><app-icon name="key" /><span class="cta-long">Ingresar a mi espacio</span
            ><span class="cta-short">Ingresar</span></a
          >
        </div>
      </header>

      <main id="content">
        <section class="public-hero welcome-main" aria-labelledby="hero-title">
          <div class="welcome-copy">
            <span class="eyebrow hero-eyebrow">{{ school.displayName | uppercase }}</span>
            <h1 id="hero-title">{{ school.tagline || school.displayName }}</h1>
            <p class="welcome-description">{{ school.welcomeMessage }}</p>
            <div class="hero-actions">
              <a class="button hero-primary" href="/Identity/Account/Login"
                >Ingresar a mi espacio <app-icon name="arrow"
              /></a>
              <a class="button outline" href="#contacto">Contactar al colegio</a>
            </div>
            <ul class="hero-facts" aria-label="Contacto rápido">
              @if (phoneHref(); as tel) {
                <li>
                  <a [href]="tel"
                    ><span class="fact-icon"><app-icon name="phone" /></span
                    >{{ school.contact.phone }}</a
                  >
                </li>
              }
              @if (school.contact.address) {
                <li>
                  <a [href]="directionsUrl()" target="_blank" rel="noopener"
                    ><span class="fact-icon"><app-icon name="pin" /></span>{{ shortAddress()
                    }}<span class="sr-only"> (abre Google Maps)</span></a
                  >
                </li>
              }
            </ul>
          </div>
          <aside class="portal-card" aria-labelledby="portal-title">
            <div class="portal-card-heading">
              <span class="portal-label">PLATAFORMA ESCOLAR</span>
              <h2 id="portal-title">Tu día a día,<br />más sencillo.</h2>
              <p>La información que necesitas, sin ir al colegio.</p>
            </div>
            <div class="portal-options">
              <div>
                <span class="icon-tile"><app-icon name="book" /></span
                ><span
                  ><strong>Notas y tareas</strong
                  ><small>Acompaña cada paso del aprendizaje.</small></span
                ><app-icon name="check" />
              </div>
              <div>
                <span class="icon-tile"><app-icon name="receipt" /></span
                ><span
                  ><strong>Pagos y recibos</strong
                  ><small>Consulta tu estado de cuenta.</small></span
                ><app-icon name="check" />
              </div>
              <div>
                <span class="icon-tile"><app-icon name="mail" /></span
                ><span
                  ><strong>Avisos del colegio</strong
                  ><small>Mantente al tanto de lo importante.</small></span
                ><app-icon name="check" />
              </div>
            </div>
            <div class="portal-card-footer">
              <app-icon name="shield" /><span>Una cuenta personal para cada integrante.</span>
            </div>
          </aside>
        </section>

        @if (hasAbout()) {
          <section id="nosotros" class="public-section" aria-labelledby="about-title">
            <div class="section-intro">
              <span class="eyebrow">NOSOTROS</span>
              <h2 id="about-title">{{ school.displayName }}</h2>
              @if (school.public.about) {
                <p>{{ school.public.about }}</p>
              }
            </div>
            @if (school.public.highlights.length) {
              <div class="public-grid">
                @for (item of school.public.highlights; track item.title) {
                  <article class="public-card">
                    <span class="icon-tile mint"><app-icon name="star" /></span>
                    <h3>{{ item.title }}</h3>
                    <p>{{ item.text }}</p>
                  </article>
                }
              </div>
            }
          </section>
        }

        @if (school.public.programs.length) {
          <section id="niveles" class="public-section" aria-labelledby="programs-title">
            <div class="section-intro">
              <span class="eyebrow">NIVELES EDUCATIVOS</span>
              <h2 id="programs-title">Formación para cada etapa</h2>
            </div>
            <div class="public-grid">
              @for (item of school.public.programs; track item.title; let i = $index) {
                <article class="public-card program-card">
                  <span class="program-number">{{ i + 1 < 10 ? '0' + (i + 1) : i + 1 }}</span>
                  <h3>{{ item.title }}</h3>
                  <p>{{ item.text }}</p>
                </article>
              }
            </div>
            @if (school.public.admissionsMessage) {
              <p class="admissions-note">
                <app-icon name="info" />{{ school.public.admissionsMessage }}
              </p>
            }
          </section>
        }

        <section
          id="plataforma"
          class="public-section platform-section"
          aria-labelledby="platform-title"
        >
          <div class="section-intro">
            <span class="eyebrow">FAMILIAS, ESTUDIANTES Y DOCENTES</span>
            <h2 id="platform-title">Más cerca de su educación.</h2>
            <p>
              Estudiantes, familias y docentes ingresan con la cuenta que entrega el colegio. Cada
              persona ve solo lo que le corresponde.
            </p>
          </div>
          <div class="public-grid feature-grid">
            @for (feature of features; track feature.title) {
              <article class="public-card feature-card">
                <span class="icon-tile" [class]="feature.tone"
                  ><app-icon [name]="feature.icon"
                /></span>
                <h3>{{ feature.title }}</h3>
                <p>{{ feature.text }}</p>
              </article>
            }
          </div>
        </section>

        <section
          id="contacto"
          class="public-section contact-section"
          aria-labelledby="contact-title"
        >
          <div class="section-intro">
            <span class="eyebrow">CONTACTO Y UBICACIÓN</span>
            <h2 id="contact-title">Estamos para ayudarte</h2>
            <p>Escríbenos, llámanos o visítanos. Con gusto te atendemos.</p>
          </div>
          <div class="contact-layout">
            <div class="contact-cards">
              @if (school.contact.address) {
                <article class="contact-tile">
                  <span class="icon-tile mint"><app-icon name="pin" /></span>
                  <div>
                    <h3>Dirección</h3>
                    <p>{{ school.contact.address }}</p>
                    <a class="text-link" [href]="directionsUrl()" target="_blank" rel="noopener"
                      >Cómo llegar <app-icon name="arrow" /><span class="sr-only">
                        (abre Google Maps)</span
                      ></a
                    >
                  </div>
                </article>
              }
              @if (school.contact.phone || school.contact.phones.length) {
                <article class="contact-tile">
                  <span class="icon-tile blue"><app-icon name="phone" /></span>
                  <div>
                    <h3>Teléfono</h3>
                    @if (school.contact.phone) {
                      <p>
                        <a class="big-link" [href]="phoneHref()">{{ school.contact.phone }}</a>
                      </p>
                    }
                    @for (phone of school.contact.phones; track phone.number) {
                      <p>
                        <span class="muted-label">{{ phone.label }}</span>
                        <a class="big-link" [href]="telOf(phone.number)">{{ phone.number }}</a>
                      </p>
                    }
                    @if (school.contact.whatsApp) {
                      <a
                        class="text-link"
                        [href]="'https://wa.me/' + school.contact.whatsApp"
                        target="_blank"
                        rel="noopener"
                        >Escribir por WhatsApp <app-icon name="arrow"
                      /></a>
                    }
                  </div>
                </article>
              }
              @if (school.contact.email) {
                <article class="contact-tile">
                  <span class="icon-tile gold"><app-icon name="mail" /></span>
                  <div>
                    <h3>Correo</h3>
                    <p>
                      <a class="big-link email-link" [href]="'mailto:' + school.contact.email">{{
                        school.contact.email
                      }}</a>
                    </p>
                  </div>
                </article>
              }
              @if (school.contact.officeHours.length) {
                <article class="contact-tile">
                  <span class="icon-tile purple"><app-icon name="clock" /></span>
                  <div>
                    <h3>Horario de atención</h3>
                    @for (line of school.contact.officeHours; track line) {
                      <p>{{ line }}</p>
                    }
                  </div>
                </article>
              }
              @if (socials().length) {
                <div class="social-row" aria-label="Redes sociales">
                  @for (item of socials(); track item.url) {
                    <a
                      class="button outline compact-button"
                      [href]="item.url"
                      target="_blank"
                      rel="noopener"
                      >{{ item.name }}</a
                    >
                  }
                </div>
              }
            </div>
            @if (mapQuery()) {
              <div class="map-card">
                @if (mapUrl(); as url) {
                  <iframe
                    [src]="url"
                    title="Ubicación en el mapa"
                    loading="lazy"
                    referrerpolicy="no-referrer-when-downgrade"
                  ></iframe>
                } @else {
                  <div class="map-placeholder">
                    <span class="map-pin" aria-hidden="true"><app-icon name="pin" /></span>
                    <h3>Nuestra ubicación</h3>
                    <p>{{ school.contact.address || mapQuery() }}</p>
                    <div class="map-actions">
                      <button class="button" type="button" (click)="showMap()">Mostrar mapa</button>
                      <a
                        class="button outline"
                        [href]="directionsUrl()"
                        target="_blank"
                        rel="noopener"
                        >Abrir en Google Maps</a
                      >
                    </div>
                    <small>Al mostrar el mapa tu navegador se conecta con Google Maps.</small>
                  </div>
                }
              </div>
            }
          </div>
        </section>

        <section class="public-band" aria-labelledby="band-title">
          <div>
            <h2 id="band-title">¿Ya tienes tu cuenta?</h2>
            <p>
              Consulta notas publicadas, tareas, asistencia, estado de cuenta y avisos del colegio.
            </p>
          </div>
          <div class="band-actions">
            <a class="button band-button" href="/Identity/Account/Login"
              >Iniciar sesión <app-icon name="arrow"
            /></a>
            <a class="band-link" href="/Identity/Account/Activate">Tengo un código de activación</a>
          </div>
        </section>
      </main>

      <footer class="public-footer">
        <div class="footer-brand">
          <div>
            <strong>{{ school.displayName }}</strong>
            <span>{{ school.tagline }}</span>
          </div>
        </div>
        <div class="footer-contact">
          @if (school.contact.address) {
            <span>{{ school.contact.address }}</span>
          }
          @if (school.contact.phone) {
            <a [href]="phoneHref()">{{ school.contact.phone }}</a>
          }
          @if (school.contact.email) {
            <a [href]="'mailto:' + school.contact.email">{{ school.contact.email }}</a>
          }
        </div>
        <div class="footer-bottom">
          <span>© {{ year }} {{ school.displayName }}</span>
          @if (school.previewEnabled) {
            <a routerLink="/preview"
              >Explorar muestra del diseño <span aria-hidden="true">↗</span></a
            >
          }
        </div>
      </footer>
    </div>
  `,
})
export class Welcome {
  readonly school = inject(InstitutionService).current;
  private readonly sanitizer = inject(DomSanitizer);
  readonly year = new Date().getFullYear();
  readonly mapUrl = signal<SafeResourceUrl | null>(null);
  readonly features = [
    {
      icon: 'star',
      tone: 'gold',
      title: 'Calificaciones',
      text: 'Resultados de cada período cuando el colegio los publica, con descarga en PDF.',
    },
    {
      icon: 'book',
      tone: 'purple',
      title: 'Tareas y entregas',
      text: 'Actividades de cada curso, entregas con archivos y retroalimentación del docente.',
    },
    {
      icon: 'check',
      tone: 'mint',
      title: 'Asistencia',
      text: 'Asistencia por curso y registro de entradas y salidas con el carnet escolar.',
    },
    {
      icon: 'wallet',
      tone: 'blue',
      title: 'Estado de cuenta',
      text: 'Cuotas, pagos y recibos para el encargado responsable.',
    },
    {
      icon: 'mail',
      tone: 'gold',
      title: 'Avisos del colegio',
      text: 'Comunicados para el grado, el curso o toda la comunidad.',
    },
    {
      icon: 'shield',
      tone: 'mint',
      title: 'Cuenta segura',
      text: 'Acceso personal con verificación en dos pasos opcional.',
    },
  ];
  readonly hasAbout = computed(
    () => !!this.school.public.about || this.school.public.highlights.length > 0,
  );
  readonly mapQuery = computed(() => this.school.contact.mapQuery || this.school.contact.address);
  readonly shortAddress = computed(() =>
    this.school.contact.address.split(',').slice(0, 2).join(','),
  );
  readonly phoneHref = computed(() =>
    this.school.contact.phone ? this.telOf(this.school.contact.phone) : null,
  );
  readonly socials = computed(() =>
    Object.entries({
      Facebook: this.school.contact.social.facebook,
      Instagram: this.school.contact.social.instagram,
      YouTube: this.school.contact.social.youTube,
      TikTok: this.school.contact.social.tikTok,
    })
      .filter(([, url]) => !!url)
      .map(([name, url]) => ({ name, url })),
  );
  readonly missing = computed(() => {
    const c = this.school.contact;
    const p = this.school.public;
    return [
      !c.phone && 'Contact.Phone',
      !c.address && 'Contact.Address',
      !c.officeHours.length && 'Contact.OfficeHours',
      !c.whatsApp && 'Contact.WhatsApp',
      !this.socials().length && 'Contact.Social',
      !p.about && 'Public.About',
      !p.programs.length && 'Public.Programs',
    ].filter((x): x is string => !!x);
  });

  telOf(number: string): string {
    return 'tel:' + this.school.contact.dialPrefix + number.replace(/[^0-9]/g, '');
  }
  directionsUrl(): string {
    return (
      'https://www.google.com/maps/dir/?api=1&destination=' + encodeURIComponent(this.mapQuery())
    );
  }
  /** Loads the embedded map on request (built only from the configured address). */
  showMap(): void {
    this.mapUrl.set(
      this.sanitizer.bypassSecurityTrustResourceUrl(
        'https://www.google.com/maps?q=' +
          encodeURIComponent(this.mapQuery()) +
          '&z=16&output=embed',
      ),
    );
  }
}
