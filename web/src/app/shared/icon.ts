import { Component, input } from '@angular/core';
@Component({
  selector: 'app-icon',
  template:
    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path [attr.d]="paths[name()] || paths[\'home\']" /></svg>',
  styles:
    ':host { display:inline-flex; width:22px; height:22px; flex-shrink:0 } svg { width:100%; height:100% }',
})
export class Icon {
  readonly name = input('home');
  readonly paths: Record<string, string> = {
    home: 'm3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1z',
    people:
      'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M16 3a4 4 0 0 1 0 8M22 21v-2a4 4 0 0 0-3-3.87M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
    book: 'M4 3h7a3 3 0 0 1 3 3v15a4 4 0 0 0-4-2H4zM14 6a3 3 0 0 1 3-3h4v16h-3a4 4 0 0 0-4 2',
    arrow: 'M5 12h14m-6-6 6 6-6 6',
    check: 'm5 12 4 4L19 6',
    shield: 'm12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6zM8 12l3 3 5-6',
    search: 'M21 21l-5-5M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',
    calendar:
      'M7 2v4m10-4v4M3 10h18M4 4h16a1 1 0 0 1 1 1v16H3V5a1 1 0 0 1 1-1M7 14h2m4 0h4m-10 4h2',
    school: 'm2 9 10-6 10 6-10 6zM6 12v6c4 3 8 3 12 0v-6M22 9v8',
    chart: 'M4 3v18h17M8 16v-5m5 5V7m5 9V4',
    star: 'm12 3 3 6 7 1-5 5 1 7-6-3-6 3 1-7-5-5 7-1z',
    mail: 'M3 5h18v14H3zM3 5l9 7 9-7',
    menu: 'M3 6h18M3 12h18M3 18h18',
    close: 'm6 6 12 12M6 18 18 6',
    logout: 'M9 3H4v18h5m5-15 6 6-6 6m-6-6h12',
    info: 'M12 11v6m0-10h.01M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
    wallet: 'M3 7h16a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2zM3 7l12-4v4M16 13.5h2',
    receipt: 'M6 2h12v20l-3-2-3 2-3-2-3 2zM9 7h6M9 11h6M9 15h4',
  };
}
