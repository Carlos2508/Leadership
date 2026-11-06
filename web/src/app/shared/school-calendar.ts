import {
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { InstitutionService } from '../core/institution';
import { todayIn } from '../core/academic-api';
import { Icon } from './icon';

@Component({
  selector: 'app-school-calendar',
  imports: [Icon],
  template: `<section
    class="card reference-calendar"
    id="calendario"
    aria-labelledby="calendar-month"
  >
    <div class="calendar-heading">
      <button class="icon-button" (click)="shift(-1)" aria-label="Mes anterior">
        <app-icon name="chevron" />
      </button>
      <h2 id="calendar-month">{{ monthTitle() }}</h2>
      <button class="icon-button" (click)="shift(1)" aria-label="Mes siguiente">
        <app-icon name="chevron" />
      </button>
    </div>
    <p class="sr-only" id="calendar-help">
      Usa las flechas para recorrer las fechas y Enter para consultar las actividades del día.
    </p>
    <div class="calendar-weekdays" aria-hidden="true">
      @for (label of weekdays; track $index) {
        <span>{{ label }}</span>
      }
    </div>
    <div class="calendar-days" aria-describedby="calendar-help">
      @for (day of days(); track day.iso) {
        <button
          [attr.data-calendar-date]="day.iso"
          [attr.tabindex]="day.iso === focusDate() ? 0 : -1"
          [class.other-month]="!day.current"
          [class.selected]="day.iso === selected()"
          [class.has-events]="count(day.iso) > 0"
          [attr.aria-pressed]="day.iso === selected()"
          [attr.aria-label]="label(day.iso)"
          (focus)="focusDate.set(day.iso)"
          (click)="choose(day.iso)"
          (keydown)="move($event, day.iso)"
        >
          {{ day.number }}
          @if (count(day.iso)) {
            <i aria-hidden="true"></i>
          }
        </button>
      }
    </div>
    <button class="calendar-today" (click)="goToday()">Ir a hoy</button>
  </section>`,
})
export class SchoolCalendar {
  readonly on = input('');
  readonly eventDates = input<string[]>([]);
  readonly selectDate = output<string>();
  private readonly school = inject(InstitutionService).current;
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly weekdays = ['L', 'M', 'M', 'J', 'V', 'S', 'D'];
  readonly month = signal({ year: new Date().getFullYear(), month: new Date().getMonth() });
  readonly selected = signal('');
  readonly focusDate = signal('');
  readonly counts = computed(() => {
    const result = new Map<string, number>();
    for (const date of this.eventDates()) result.set(date, (result.get(date) ?? 0) + 1);
    return result;
  });
  readonly monthTitle = computed(() =>
    new Intl.DateTimeFormat(this.school.locale.culture, {
      month: 'long',
      year: 'numeric',
      timeZone: 'UTC',
    })
      .formatToParts(new Date(Date.UTC(this.month().year, this.month().month, 15)))
      .filter((part) => part.type === 'month' || part.type === 'year')
      .map((part) => part.value)
      .join(' '),
  );
  readonly days = computed(() => {
    const month = this.month();
    const first = new Date(Date.UTC(month.year, month.month, 1));
    const start = new Date(first);
    start.setUTCDate(1 - ((first.getUTCDay() + 6) % 7));
    const last = new Date(Date.UTC(month.year, month.month + 1, 0)).getUTCDate();
    const slots = Math.ceil((((first.getUTCDay() + 6) % 7) + last) / 7) * 7;
    return Array.from({ length: slots }, (_, index) => {
      const date = new Date(start);
      date.setUTCDate(start.getUTCDate() + index);
      return {
        iso: date.toISOString().slice(0, 10),
        number: date.getUTCDate(),
        current: date.getUTCMonth() === month.month,
      };
    });
  });
  constructor() {
    effect(() => {
      const value = this.on() || todayIn(this.school.locale.timeZone);
      this.setMonth(value);
      this.selected.set(value);
      this.focusDate.set(value);
    });
  }
  private setMonth(value: string): void {
    const [year, month] = value.split('-').map(Number);
    this.month.set({ year, month: month - 1 });
  }
  count(date: string): number {
    return this.counts().get(date) ?? 0;
  }
  label(date: string): string {
    const text = new Intl.DateTimeFormat(this.school.locale.culture, {
      dateStyle: 'full',
      timeZone: 'UTC',
    }).format(new Date(date + 'T12:00:00Z'));
    const count = this.count(date);
    return (
      text +
      (count ? `, ${count} ${count === 1 ? 'actividad' : 'actividades'}` : ', sin actividades')
    );
  }
  shift(direction: number): void {
    const current = this.month();
    const next = new Date(Date.UTC(current.year, current.month + direction, 1));
    this.month.set({ year: next.getUTCFullYear(), month: next.getUTCMonth() });
    this.focusDate.set(next.toISOString().slice(0, 10));
  }
  choose(date: string): void {
    this.selected.set(date);
    this.focusDate.set(date);
    this.setMonth(date);
    this.selectDate.emit(date);
  }
  goToday(): void {
    this.choose(todayIn(this.school.locale.timeZone));
  }
  move(event: KeyboardEvent, iso: string): void {
    const distance: Record<string, number> = {
      ArrowLeft: -1,
      ArrowRight: 1,
      ArrowUp: -7,
      ArrowDown: 7,
    };
    if (!(event.key in distance)) return;
    event.preventDefault();
    const date = new Date(iso + 'T12:00:00Z');
    date.setUTCDate(date.getUTCDate() + distance[event.key]);
    const next = date.toISOString().slice(0, 10);
    this.focusDate.set(next);
    this.setMonth(next);
    requestAnimationFrame(() =>
      this.host.nativeElement
        .querySelector<HTMLButtonElement>(`[data-calendar-date="${next}"]`)
        ?.focus(),
    );
  }
}
