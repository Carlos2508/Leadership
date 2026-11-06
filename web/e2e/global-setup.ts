import { execFileSync } from 'node:child_process';
import { join, resolve } from 'node:path';
import { rmSync } from 'node:fs';
import { tmpdir } from 'node:os';

export default function setup(): () => void {
  const root = resolve(__dirname, '../..');
  const project = 'tests/SchoolPlatform.TestHarness';
  const env = { ...process.env, Logging__LogLevel__Default: 'Warning' };
  const run = (operation: string) => {
    try {
      execFileSync('dotnet', ['run', '--project', project, '--', operation], {
        cwd: root,
        env,
        stdio: 'pipe',
        timeout: 120_000,
      });
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
    const database = process.env['SCHOOLPLATFORM_E2E_DATABASE']!;
    if (/^SchoolPlatform_E2E_[a-f0-9]{32}$/.test(database))
      rmSync(join(tmpdir(), database + '_files'), { recursive: true, force: true });
    if (/^SchoolPlatform_E2E_[a-f0-9]{32}$/.test(database))
      rmSync(join(tmpdir(), database + '_migration'), { recursive: true, force: true });
  };
}
