import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';

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
  return () => run('cleanup');
}
