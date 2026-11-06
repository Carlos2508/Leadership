import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { DOCUMENT } from '@angular/common';

export interface Institution {
  displayName: string;
  shortName: string;
  campusLabel: string;
  tagline: string;
  welcomeMessage: string;
  contact: {
    phone: string;
    email: string;
    address: string;
    website: string;
    dialPrefix: string;
    phones: { label: string; number: string }[];
    whatsApp: string;
    officeHours: string[];
    mapQuery: string;
    social: { facebook: string; instagram: string; youTube: string; tikTok: string };
  };
  public: {
    aboutTitle: string;
    digitalTitle: string;
    digitalText: string;
    questionsTitle: string;
    contactTitle: string;
    contactText: string;
    heroTitle: string;
    heroAccent: string;
    heroDescription: string;
    missionTitle: string;
    missionAccent: string;
    missionText: string;
    visionTitle: string;
    visionText: string;
    lifeTitle: string;
    lifeText: string;
    communityTitle: string;
    communityText: string;
    admissionsTitle: string;
    admissionSteps: { title: string; text: string }[];
    questions: { title: string; text: string }[];
    about: string;
    highlights: { title: string; text: string }[];
    programs: { title: string; text: string }[];
    admissionsMessage: string;
  };
  assets: {
    heroImage: string;
    studentImage: string;
    communityImage: string;
    mainLogo: string;
    loginLogo: string;
    receiptLogo: string;
    reportCardLogo: string;
    favicon: string;
  };
  theme: { deep: string; primary: string; accent: string; surface: string };
  locale: { culture: string; timeZone: string; currency: string };
  terms: { studentPlural: string; guardianPlural: string; tuition: string; period: string };
  previewEnabled: boolean;
}
@Injectable({ providedIn: 'root' })
export class InstitutionService {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);
  private value?: Institution;
  get current(): Institution {
    if (!this.value) throw new Error('La configuración institucional no está disponible.');
    return this.value;
  }
  async load(): Promise<void> {
    this.value = await firstValueFrom(this.http.get<Institution>('/api/v1/bootstrap'));
    const institution = this.value;
    this.document.title = institution.displayName;
    this.document.documentElement.lang = institution.locale.culture;
    for (const [key, value] of Object.entries(institution.theme))
      this.document.documentElement.style.setProperty(`--${key}`, value);
    this.document.querySelector<HTMLLinkElement>('link[rel="icon"]')!.href =
      institution.assets.favicon;
  }
}
