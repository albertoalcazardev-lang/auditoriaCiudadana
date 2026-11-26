import { createHash, X509Certificate } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { defineConfig } from '@playwright/test';

const certPath = join(homedir(), '.config/vyt-systems/https/localhost.pem');
const spkiArgs = existsSync(certPath)
  ? [
      `--ignore-certificate-errors-spki-list=${createHash('sha256')
        .update(
          new X509Certificate(readFileSync(certPath)).publicKey.export({
            type: 'spki',
            format: 'der',
          }),
        )
        .digest('base64')}`,
    ]
  : [];

export default defineConfig({
  testDir: './tests',
  outputDir: './node_modules/.tmp/auth-tests',
  use: {
    baseURL: 'https://localhost:5173',
    trace: 'off',
    screenshot: 'off',
    video: 'off',
    launchOptions: spkiArgs.length > 0 ? { args: spkiArgs } : undefined,
  },
});
