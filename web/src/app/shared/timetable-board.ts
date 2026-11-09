import { Component, ViewEncapsulation, input, output, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { BoardCell, TimePeriod, clock, weekdayNames } from '../core/timetables';
import { Icon } from './icon';
@Component({
  selector: 'app-timetable-board',
  imports: [NgTemplateOutlet, Icon],
  // Global (unencapsulated) like the rest of the design system, but loaded only with timetable pages.
  styleUrl: '../../styles/_timetable-board.scss',
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="time-day-tabs" aria-label="Día del horario">
      @for (day of days(); track day) {
        <button
          type="button"
          [attr.aria-pressed]="activeDay() === day"
          (click)="selectedDay.set(day)"
        >
          {{ names[day] }}<span>{{ count(day) }}</span>
        </button>
      }
    </div>
    <div
      class="time-board"
      [style.--time-days]="days().length"
      role="region"
      aria-label="Horario semanal"
      tabindex="0"
    >
      <div class="time-board-corner">PERÍODO</div>
      @for (day of days(); track day) {
        <div class="time-board-day">
          {{ names[day] }}<small>{{ count(day) }} {{ count(day) === 1 ? 'clase' : 'clases' }}</small>
        </div>
      }
      @for (period of periods(); track period.id) {
        <div class="time-board-hour">
          <strong>{{ clock(period.start) }}</strong
          ><span>{{ clock(period.end) }}</span
          ><small>{{ period.name }}</small>
        </div>
        @for (day of days(); track day) {
          <div class="time-board-space" [attr.data-time-cell]="day + '-' + period.id">
            <ng-container *ngTemplateOutlet="block; context: { period, day }" />
          </div>
        }
      }
    </div>
    <div class="time-agenda">
      @for (period of periods(); track period.id) {
        <div class="time-agenda-row">
          <div class="time-board-hour">
            <strong>{{ clock(period.start) }}</strong
            ><span>{{ clock(period.end) }}</span>
          </div>
          <ng-container *ngTemplateOutlet="block; context: { period, day: activeDay() }" />
        </div>
      }
    </div>
    <ng-template #block let-period="period" let-day="day">
      @if (cell(period.id, day); as item) {
        @if (editing()) {
          <button
            type="button"
            class="time-block"
            [class.time-break]="item.kind === 'Break'"
            [class.time-warning]="item.warning"
            (click)="selectCell.emit({ period, day })"
            [attr.aria-label]="
              names[day] + ' ' + clock(period.start) + ': ' + item.title + ', editar bloque'
            "
          >
            <ng-container *ngTemplateOutlet="copy; context: { $implicit: item }" />
          </button>
        } @else {
          <article class="time-block" [class.time-break]="item.kind === 'Break'">
            <ng-container *ngTemplateOutlet="copy; context: { $implicit: item }" />
          </article>
        }
      } @else if (editing()) {
        <button
          type="button"
          class="time-empty"
          (click)="selectCell.emit({ period, day })"
          [attr.aria-label]="'Asignar ' + names[day] + ' ' + clock(period.start)"
        >
          <app-icon name="plus" /><span>Asignar</span>
        </button>
      } @else {
        <span class="time-free">Sin clase programada</span>
      }
    </ng-template>
    <ng-template #copy let-item>
      <span class="time-block-kind"
        >{{ item.kind === 'Break' ? 'DESCANSO' : 'CLASE' }}
        @if (item.warning) {
          · Revisar
        }
      </span>
      <strong>{{ item.title }}</strong>
      @if (item.teacher) {
        <span>{{ item.teacher }}</span>
      }
      @if (item.room) {
        <small><app-icon name="school" />{{ item.room }}</small>
      }
    </ng-template>
  `,
})
export class TimetableBoard {
  readonly days = input.required<number[]>();
  readonly periods = input.required<TimePeriod[]>();
  readonly cells = input.required<BoardCell[]>();
  readonly editing = input(false);
  readonly selectCell = output<{ period: TimePeriod; day: number }>();
  readonly selectedDay = signal(1);
  readonly names = weekdayNames;
  readonly clock = clock;
  activeDay(): number {
    return this.days().includes(this.selectedDay()) ? this.selectedDay() : this.days()[0];
  }
  cell(period: string, day: number) {
    return this.cells().find((c) => c.periodId === period && c.day === day);
  }
  count(day: number) {
    return this.cells().filter((c) => c.day === day && c.kind === 'Lesson').length;
  }
}
