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
    user: 'M20 21v-2a7 7 0 0 0-14 0v2M17 7a5 5 0 1 1-10 0 5 5 0 0 1 10 0',
    play: 'M9 7l8 5-8 5zM22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
    compass: 'm16 8-2.5 5.5L8 16l2.5-5.5zM22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
    heart: 'M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8L12 21l8.8-8.6a5.5 5.5 0 0 0 0-7.8',
    plus: 'M12 5v14M5 12h14',
    'phone-call': 'M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1 1 .3 2 .7 2.9a2 2 0 0 1-.5 2.1L8 10a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.5c.9.4 1.9.6 2.9.7a2 2 0 0 1 1.7 2',
    message: 'M21 11.5a8.4 8.4 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.4 8.4 0 0 1-3.8-.9L3 21l1.9-5.7a8.4 8.4 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.4 8.4 0 0 1 3.8-.9H13a8.5 8.5 0 0 1 8 8v.5',
    instagram: 'M7 2h10a5 5 0 0 1 5 5v10a5 5 0 0 1-5 5H7a5 5 0 0 1-5-5V7a5 5 0 0 1 5-5M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0M17.5 6.5h.01',
    bell: 'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4',
    folder: 'M3 5h6l2 3h10v13H3z',
    megaphone: 'm3 10 18-7v16L3 13zM7 14l2 7h4l-2-6',
    chevron: 'm8 4 8 8-8 8',
    zap: 'm13 2-9 12h7l-1 8 10-12h-7z',
    clipboard: 'M9 4H5v18h14V4h-4M9 2h6v5H9zM8 13l3 3 5-5',
    refresh: 'M20 7v5h-5M4 17v-5h5M6 7a7 7 0 0 1 12-2l2 3M4 16l2 3a7 7 0 0 0 12-2',
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
    scan: 'M3 7V5a2 2 0 0 1 2-2h2M17 3h2a2 2 0 0 1 2 2v2M21 17v2a2 2 0 0 1-2 2h-2M7 21H5a2 2 0 0 1-2-2v-2M7 8v8M10 8v8M13 8v8M17 8v8',
    phone: 'M7 2h10a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1zM11 18h2',
    clock: 'M12 7v5l3 2M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
    card: 'M3 5h18a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1zM7 10h0M6 15h5M14 9h4M14 13h4M8.5 10a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0',
    download: 'M12 3v12m-5-5 5 5 5-5M4 19h16',
    pin: 'M12 22s7-6.2 7-12a7 7 0 1 0-14 0c0 5.8 7 12 7 12zM12 12.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5',
    users:
      'M17 21v-2a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v2M14 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0M21 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8',
    key: 'M15 7a4 4 0 1 1-3.9 4.9L3 20v2h3v-2h2v-2h2l1.1-1.1A4 4 0 0 1 15 7zM16 10h.01',
  };
}
