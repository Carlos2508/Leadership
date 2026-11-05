import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InstitutionService } from '../core/institution';
import { Icon } from '../shared/icon';
@Component({
  imports: [RouterLink, Icon],
  template: `
    <a class="skip-link" href="#content">Saltar al contenido</a>
    <div class="welcome-page">
      <header class="welcome-header">
        <a class="brand" routerLink="/"
          ><img [src]="school.assets.mainLogo" alt="" /><span
            >{{ school.shortName }}<small>{{ school.campusLabel }}</small></span
          ></a
        >
        <a href="/Identity/Account/Login" class="button outline"
          >Ingresar <app-icon name="arrow"
        /></a>
      </header>
      <main id="content" class="welcome-main">
        <section class="welcome-copy">
          <span class="eyebrow">BIENVENIDOS A NUESTRA COMUNIDAD</span>
          <h1>{{ school.tagline || school.displayName }}</h1>
          <p class="welcome-description">{{ school.welcomeMessage }}</p>
          <p class="welcome-institution">{{ school.displayName }}</p>
          <a class="button" href="/Identity/Account/Login"
            >Ingresar a mi espacio <app-icon name="arrow"
          /></a>
          <p class="access-help">Usa la cuenta que te proporcionó el colegio.</p>
        </section>
        <section class="welcome-art" aria-label="Comunidad educativa">
          <div class="orb orb-one"></div>
          <div class="orb orb-two"></div>
          <div class="school-seal">
            <img [src]="school.assets.mainLogo" [alt]="school.displayName" />
          </div>
          <div class="floating-card card-learning">
            <span class="icon-tile mint"><app-icon name="book" /></span>
            <div><strong>Aprendemos juntos</strong><small>Cada paso cuenta</small></div>
          </div>
          <div class="floating-card card-community">
            <span class="icon-tile gold"><app-icon name="people" /></span>
            <div><strong>Una comunidad</strong><small>Conectados para crecer</small></div>
          </div>
          <span class="decorative-star" aria-hidden="true">✦</span>
        </section>
      </main>
      <footer class="welcome-footer">
        <span>{{ school.displayName }}</span>
        <div class="contact-inline">
          @if (school.contact.phone) {
            <span>{{ school.contact.phone }}</span>
          }
          @if (school.contact.email) {
            <span>{{ school.contact.email }}</span>
          }
          @if (school.contact.address) {
            <span>{{ school.contact.address }}</span>
          }
        </div>
        @if (school.previewEnabled) {
          <a routerLink="/preview">Explorar muestra del diseño <span aria-hidden="true">↗</span></a>
        }
      </footer>
    </div>
  `,
})
export class Welcome {
  readonly school = inject(InstitutionService).current;
}
