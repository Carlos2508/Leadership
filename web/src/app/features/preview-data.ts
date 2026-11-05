import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
export interface PreviewStudent {
  code: string;
  name: string;
  grade: string;
  status: string;
  initials: string;
}
export interface PreviewData {
  isSample: boolean;
  academicYear: string;
  attendance: { month: string; value: number }[];
  students: PreviewStudent[];
}
@Injectable({ providedIn: 'root' })
export class PreviewService {
  private readonly http = inject(HttpClient);
  readonly data = signal<PreviewData | null>(null);
  readonly error = signal(false);
  async load(): Promise<void> {
    if (this.data()) return;
    this.error.set(false);
    try {
      this.data.set(await firstValueFrom(this.http.get<PreviewData>('/api/v1/preview')));
    } catch {
      this.error.set(true);
    }
  }
}
