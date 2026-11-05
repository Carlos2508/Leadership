import { Pipe, PipeTransform } from '@angular/core';

/** «Ciclo 2027» from «2027», without repeating the word when the configured label already includes it. */
@Pipe({ name: 'cycle' })
export class CyclePipe implements PipeTransform {
  transform(label: string | null | undefined, lower = false): string {
    const value = (label ?? '').trim();
    if (!value) return '';
    if (/^ciclo\b/i.test(value)) return lower ? value.charAt(0).toLowerCase() + value.slice(1) : value;
    return `${lower ? 'ciclo' : 'Ciclo'} ${value}`;
  }
}
