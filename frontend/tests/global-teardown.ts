import { execSync } from 'node:child_process';
import { resolve } from 'node:path';

const ROOT = resolve(process.cwd(), '..');

export default function globalTeardown() {
  if (!process.env.CI) {
    execSync('docker compose down --volumes', { cwd: ROOT, stdio: 'inherit' });
  }
}
