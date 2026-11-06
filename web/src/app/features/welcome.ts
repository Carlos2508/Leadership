import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { ViewportScroller } from '@angular/common';
import { InstitutionService } from '../core/institution';
import { Icon } from '../shared/icon';

@Component({
  imports: [Icon, RouterLink],
  templateUrl: './welcome.html',
  host: { '(document:keydown.escape)': 'menuOpen.set(false)' },
})
export class Welcome implements AfterViewInit, OnDestroy {
  readonly school = inject(InstitutionService).current;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly viewport = inject(ViewportScroller);
  private observer?: IntersectionObserver;
  readonly year = new Date().getFullYear();
  readonly menuOpen = signal(false);
  readonly activeSection = signal('inicio');
  readonly mapUrl = signal<SafeResourceUrl | null>(null);
  readonly pillarIcons = ['school', 'users', 'chart', 'star'];
  readonly hasAbout = computed(
    () =>
      !!(
        this.school.public.missionText ||
        this.school.public.about ||
        this.school.public.visionText
      ),
  );
  readonly socials = computed(() =>
    Object.entries({
      Instagram: this.school.contact.social.instagram,
      Facebook: this.school.contact.social.facebook,
      YouTube: this.school.contact.social.youTube,
      TikTok: this.school.contact.social.tikTok,
    })
      .filter(([, url]) => !!url)
      .map(([name, url]) => ({
        name,
        url,
        handle: '@' + new URL(url).pathname.split('/').filter(Boolean).at(-1),
      })),
  );
  readonly navigation = computed(() =>
    [
      { id: 'inicio', label: 'Inicio', visible: true },
      { id: 'nosotros', label: 'Nosotros', visible: this.hasAbout() },
      {
        id: 'vida-escolar',
        label: 'Vida Escolar',
        visible: !!this.school.public.lifeText || !!this.school.public.programs.length,
      },
      {
        id: 'admision',
        label: 'Admisión',
        visible:
          !!this.school.public.admissionsMessage || !!this.school.public.admissionSteps.length,
      },
      { id: 'comunidad', label: 'Comunidad', visible: !!this.school.public.communityText },
      {
        id: 'contacto',
        label: 'Contacto',
        visible: !!(
          this.school.contact.address ||
          this.school.contact.mapQuery ||
          this.school.contact.phone ||
          this.school.contact.email ||
          this.school.contact.whatsApp
        ),
      },
    ].filter((item) => item.visible),
  );
  readonly admissionLink = computed(() =>
    this.school.contact.whatsApp
      ? 'https://wa.me/' + this.school.contact.whatsApp
      : this.school.contact.email
        ? this.mailTo('Información de admisiones')
        : this.school.contact.phone
          ? this.phoneHref()
          : '',
  );
  ngAfterViewInit(): void {
    this.viewport.setOffset(() => [
      0,
      (this.host.nativeElement.querySelector('.site-header')?.getBoundingClientRect().height ??
        80) + 16,
    ]);
    this.observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries)
          if (entry.isIntersecting) this.activeSection.set(entry.target.id);
      },
      { rootMargin: '-100px 0px -55% 0px', threshold: 0 },
    );
    for (const item of this.navigation()) {
      const element = this.host.nativeElement.querySelector('#' + item.id);
      if (element) this.observer.observe(element);
    }
    const hash = decodeURIComponent(location.hash.slice(1));
    if (this.navigation().some((item) => item.id === hash))
      requestAnimationFrame(() =>
        this.host.nativeElement.querySelector('#' + hash)?.scrollIntoView(),
      );
  }
  ngOnDestroy(): void {
    this.observer?.disconnect();
    this.viewport.setOffset([0, 0]);
  }
  navigate(id: string): void {
    this.activeSection.set(id);
    this.menuOpen.set(false);
  }
  phoneHref(): string {
    return 'tel:' + this.school.contact.dialPrefix + this.school.contact.phone.replace(/\D/g, '');
  }
  mailTo(subject = ''): string {
    return (
      'mailto:' +
      this.school.contact.email +
      (subject ? '?subject=' + encodeURIComponent(subject) : '')
    );
  }
  directionsUrl(): string {
    return (
      'https://www.google.com/maps/dir/?api=1&destination=' +
      encodeURIComponent(this.school.contact.mapQuery || this.school.contact.address)
    );
  }
  showMap(): void {
    this.mapUrl.set(
      this.sanitizer.bypassSecurityTrustResourceUrl(
        'https://www.google.com/maps?q=' +
          encodeURIComponent(this.school.contact.mapQuery || this.school.contact.address) +
          '&z=16&output=embed',
      ),
    );
  }
}
