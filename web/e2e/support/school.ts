import { expect, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';

export const reason = 'Preparación sintética del recorrido de experiencia y flujos.';
export async function post(page: Page, path: string, data: object) {
  const { token } = await (await page.request.get('/api/v1/antiforgery')).json();
  const response = await page.request.post(path, { data, headers: { 'X-XSRF-TOKEN': token } });
  expect(response.status(), path).toBeLessThan(300);
  return response.status() === 204 ? null : response.json();
}
export async function login(page: Page, ensureSchoolAccess = true): Promise<void> {
  await page.goto('/Identity/Account/Login');
  await enterCredentials(page);
  await expect(
    page.getByRole('heading', { name: /^¡Hola,/ }),
  ).toBeVisible();
  const session = await (await page.request.get('/api/v1/session')).json();
  if (ensureSchoolAccess && !session.permissions.includes('academic.manage')) {
    const accounts = await (await page.request.get('/api/v1/access/accounts')).json();
    const me = accounts.items.find((account: { id: string }) => account.id === session.userId);
    await post(page, `/api/v1/access/accounts/${me.id}/profile`, {
      profile: 'administrator',
      version: me.version,
      reason,
    });
    await login(page);
  }
}
export async function enterCredentials(page: Page): Promise<void> {
  await page
    .getByLabel('Usuario', { exact: true })
    .fill(process.env['SCHOOLPLATFORM_E2E_USERNAME']!);
  await page
    .getByLabel('Contraseña', { exact: true })
    .fill(process.env['SCHOOLPLATFORM_E2E_PASSWORD']!);
  await page.getByRole('button', { name: 'Ingresar a mi espacio' }).click();
}
export async function year(page: Page, number: number) {
  const years = await (await page.request.get('/api/v1/academic/years')).json();
  const existing = years.find((item: { startsOn: string }) => item.startsOn === `${number}-01-10`);
  if (existing) return existing.id as string;
  const id = (
    await post(page, '/api/v1/academic/years', {
      label: `Recorridos ${number}`,
      startsOn: `${number}-01-10`,
      endsOn: `${number}-10-30`,
    })
  ).id as string;
  await post(page, `/api/v1/academic/years/${id}/periods`, {
    startsOn: `${number}-01-10`,
    endsOn: `${number}-03-10`,
  });
  const detail = await (await page.request.get(`/api/v1/academic/years/${id}`)).json();
  await post(page, `/api/v1/academic/years/${id}/status`, {
    version: detail.year.version,
    status: 'EnrollmentOpen',
    reason,
  });
  return id;
}
export async function courseFixture(page: Page, number: number) {
  await login(page);
  const suffix = randomUUID().slice(0, 7);
  const yearId = await year(page, number);
  const grade = (
    await post(page, '/api/v1/academic/grades', {
      code: 'X' + suffix,
      name: 'Grado ' + suffix,
      stage: '',
      sequence: 85,
    })
  ).id as string;
  const subject = (
    await post(page, '/api/v1/academic/subjects', { code: 'X' + suffix, name: 'Curso ' + suffix })
  ).id;
  const students: string[] = [];
  for (const givenNames of ['Ana', 'Beto']) {
    const student = (
      await post(page, '/api/v1/people', {
        givenNames,
        familyNames: 'Recorrido ' + suffix,
        contacts: [],
        studentCode: givenNames.slice(0, 1) + suffix,
        isGuardian: false,
      })
    ).id as string;
    await post(page, '/api/v1/enrollments', {
      studentId: student,
      academicYearId: yearId,
      gradeLevelId: grade,
      sectionId: null,
      enrolledOn: `${number}-01-10`,
    });
    students.push(student);
  }
  await post(page, `/api/v1/years/${yearId}/courses`, {
    gradeLevelId: grade,
    sectionId: null,
    subjectIds: [subject],
  });
  const course = (
    await (await page.request.get(`/api/v1/years/${yearId}/courses?gradeId=${grade}`)).json()
  )[0].id as string;
  return { yearId, course, students, suffix, grade };
}
