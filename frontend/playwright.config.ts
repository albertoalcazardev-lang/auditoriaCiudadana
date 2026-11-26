import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  outputDir: './node_modules/.tmp/auth-tests',
  use: { baseURL: 'https://localhost:5173', trace: 'off', screenshot: 'off', video: 'off' },
});
