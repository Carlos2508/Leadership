import { defineConfig } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';
// Set once in the coordinator; workers inherit these values when they load this configuration.
process.env['SCHOOLPLATFORM_E2E_DATABASE'] ??=
  'SchoolPlatform_E2E_' + randomUUID().replaceAll('-', '');
process.env['SCHOOLPLATFORM_E2E_USERNAME'] ??= 'synthetic-browser-owner';
process.env['SCHOOLPLATFORM_E2E_PASSWORD'] ??= 'Test!Aa1-' + randomBytes(24).toString('base64url');
const connection =
  (process.env['SCHOOLPLATFORM_E2E_SQL'] ??
    'Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true') +
  ';Database=' +
  process.env['SCHOOLPLATFORM_E2E_DATABASE'];
export default defineConfig({
  globalSetup: './e2e/global-setup.ts',
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 30_000,
  reporter: [['list']],
  use: { baseURL: 'http://127.0.0.1:5085', trace: 'retain-on-failure' },
  projects: [
    { name: 'desktop', use: { browserName: 'chromium', viewport: { width: 1440, height: 1000 } } },
    {
      name: 'mobile',
      use: {
        browserName: 'chromium',
        viewport: { width: 390, height: 844 },
        isMobile: true,
        hasTouch: true,
      },
    },
  ],
  webServer: {
    command:
      'dotnet run --project ../src/SchoolPlatform.Host --no-launch-profile --urls http://127.0.0.1:5085',
    url: 'http://127.0.0.1:5085/health/live',
    timeout: 60_000,
    reuseExistingServer: false,
    stdout: 'pipe',
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      Logging__LogLevel__Default: 'Warning',
      ConnectionStrings__SchoolDatabase: connection,
    },
  },
});
