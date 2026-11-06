import { execFileSync } from 'node:child_process';
import { join, resolve } from 'node:path';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';

export default function setup(): () => void {
  const root = resolve(__dirname, '../..');
  const project = 'tests/SchoolPlatform.TestHarness';
  const env = { ...process.env, Logging__LogLevel__Default: 'Warning' };
  const database = process.env['SCHOOLPLATFORM_E2E_DATABASE']!;
  if (!/^SchoolPlatform_E2E_[a-f0-9]{32}$/.test(database))
    throw new Error('Invalid owned E2E database name.');
  const manifest = join(root, '.local/qa/e2e-runs', database + '.json');
  mkdirSync(join(root, '.local/qa/e2e-runs'), { recursive: true });
  writeFileSync(
    manifest,
    JSON.stringify({
      database,
      purpose: 'playwright-fixture',
      startedAt: new Date().toISOString(),
    }),
  );
  const run = (operation: string) => {
    try {
      execFileSync(
        'dotnet',
        [
          'run',
          ...(operation === 'cleanup' ? ['--no-build'] : []),
          '--project',
          project,
          '--',
          operation,
        ],
        {
          cwd: root,
          env,
          stdio: 'pipe',
          timeout: 120_000,
        },
      );
    } catch {
      throw new Error(
        `No se pudo ${operation === 'prepare' ? 'preparar' : 'eliminar'} la base SQL aislada de las pruebas de navegador. Revisa la instancia y los permisos de pruebas.`,
      );
    }
  };
  run('prepare');
  return () => {
    run('cleanup');
    // Remove only the private upload folder of this run's own E2E database.
    if (/^SchoolPlatform_E2E_[a-f0-9]{32}$/.test(database))
      rmSync(join(tmpdir(), database + '_files'), { recursive: true, force: true });
    if (/^SchoolPlatform_E2E_[a-f0-9]{32}$/.test(database))
      rmSync(join(tmpdir(), database + '_migration'), { recursive: true, force: true });
    rmSync(manifest, { force: true });
  };
}
