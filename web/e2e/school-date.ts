import type { Page } from '@playwright/test';

// Commands use the installation's civil date, even after midnight UTC.
export async function schoolToday(page: Page): Promise<string> {
  const { locale } = await (await page.request.get('/api/v1/bootstrap')).json();
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: locale.timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
}
