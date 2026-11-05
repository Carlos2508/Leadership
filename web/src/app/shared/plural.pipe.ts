import { Pipe, PipeTransform } from '@angular/core';

/** «1 cargo» / «3 cargos». Usage: {{ count | plural: 'cargo' : 'cargos' }}. */
export function plural(count: number, singular: string, pluralForm: string): string {
  return `${count} ${count === 1 ? singular : pluralForm}`;
}

@Pipe({ name: 'plural' })
export class PluralPipe implements PipeTransform {
  transform(count: number | null | undefined, singular: string, pluralForm: string): string {
    return plural(count ?? 0, singular, pluralForm);
  }
}
