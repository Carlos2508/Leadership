import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccessApi } from '../core/access-api';

/** Staff directory: print the cards of every active staff member for an explicit cycle (people.manage). */
@Component({
  selector: 'app-staff-cards',
  imports: [FormsModule],
  template: `
    <section class="card staff-cards" aria-labelledby="staff-cards-title">
      <div>
        <h2 id="staff-cards-title">Carnets del personal</h2>
        <p class="muted-copy">Todo el personal activo, ordenado por apellido. Los encargados no llevan carnet.</p>
      </div>
      @if (error()) {
        <p class="feedback error-feedback" role="alert">{{ error() }}</p>
      } @else if (years().length) {
        <div class="staff-cards-actions">
          <label class="select-field"
            ><span>Ciclo</span
            ><select name="staffCardYear" [(ngModel)]="yearId">
              @for (y of years(); track y.id) {
                <option [value]="y.id">{{ y.label }}</option>
              }
            </select></label
          >
          <a class="button outline compact-button" [href]="url('sheet')" target="_blank" rel="noopener">Carnets en carta</a>
          <a class="button outline compact-button" [href]="url('card')" target="_blank" rel="noopener">Carnets en tarjeta</a>
        </div>
      } @else if (loaded()) {
        <p class="muted-copy">No hay un ciclo con inscripción abierta o en curso para imprimir carnets.</p>
      }
    </section>
  `,
})
export class StaffCards {
  private readonly api = inject(AccessApi);
  readonly years = signal<{ id: string; label: string }[]>([]);
  readonly loaded = signal(false);
  readonly error = signal('');
  yearId = '';

  constructor() {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      const years = await this.api.get<{ id: string; label: string }[]>('/api/v1/cards/years');
      this.yearId = years[0]?.id ?? '';
      this.years.set(years);
    } catch (e) {
      this.error.set(this.api.message(e));
    } finally {
      this.loaded.set(true);
    }
  }

  url(layout: string): string {
    return '/api/v1/staff/cards.pdf?' + new URLSearchParams({ yearId: this.yearId, layout });
  }
}
