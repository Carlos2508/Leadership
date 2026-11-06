import { defineConfig } from '@playwright/test';
import { randomBytes, randomUUID } from 'node:crypto';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
// Set once in the coordinator; workers inherit these values when they load this configuration.
process.env['SCHOOLPLATFORM_E2E_DATABASE'] ??=
  'SchoolPlatform_E2E_' + randomUUID().replaceAll('-', '');
process.env['SCHOOLPLATFORM_E2E_USERNAME'] ??= 'synthetic-browser-owner';
process.env['SCHOOLPLATFORM_E2E_PASSWORD'] ??= 'Test!Aa1-' + randomBytes(24).toString('base64url');
// Isolate intermediates as well as binaries: simultaneous SQL/E2E builds must never rewrite each other's obj files.
process.env['UseArtifactsOutput'] = 'true';
process.env['ArtifactsPath'] = join(
  __dirname,
  '../.local/qa/e2e-builds',
  process.env['SCHOOLPLATFORM_E2E_DATABASE']!,
);
delete process.env['BaseOutputPath'];
const connection =
  (process.env['SCHOOLPLATFORM_E2E_SQL'] ??
    'Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true') +
  ';Database=' +
  process.env['SCHOOLPLATFORM_E2E_DATABASE'];
const testPort = process.env['SCHOOLPLATFORM_E2E_PORT'] ?? '5085';
export default defineConfig({
  globalSetup: './e2e/global-setup.ts',
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 30_000,
  reporter: [['list']],
  use: { baseURL: `http://127.0.0.1:${testPort}`, trace: 'retain-on-failure' },
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
    command: `dotnet run --project ../src/SchoolPlatform.Host --no-launch-profile --urls http://127.0.0.1:${testPort}${process.env['SCHOOLPLATFORM_E2E_MIGRATION'] === '1' ? ' -- --migration-assistant' : ''}`,
    url: `http://127.0.0.1:${testPort}/health/live`,
    timeout: 60_000,
    reuseExistingServer: false,
    stdout: 'pipe',
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      Logging__LogLevel__Default: 'Warning',
      ConnectionStrings__SchoolDatabase: connection,
      // Uploaded files of the browser tests go to a private temporary folder, never to the installation storage.
      Storage__Root: join(tmpdir(), process.env['SCHOOLPLATFORM_E2E_DATABASE']! + '_files'),
      MigrationAssistant__Enabled:
        process.env['SCHOOLPLATFORM_E2E_MIGRATION'] === '1' ? 'true' : 'false',
      MigrationAssistant__LegacyConfigPath: process.env['SCHOOLPLATFORM_E2E_LEGACY_CONFIG'] ?? '',
      MigrationAssistant__StorageRoot: join(
        tmpdir(),
        process.env['SCHOOLPLATFORM_E2E_DATABASE']! + '_migration',
      ),
    },
  },
});
