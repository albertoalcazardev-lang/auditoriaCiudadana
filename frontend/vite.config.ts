import { readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig(({ command }) => {
  const certDir = join(homedir(), '.config/vyt-systems/https');
  const cert = command === 'serve' ? readFileSync(join(certDir, 'localhost.pem')) : undefined;
  return {
    plugins: [react(), tailwindcss()],
    server: {
      host: 'localhost',
      port: 5173,
      strictPort: true,
      https: cert && { cert, key: readFileSync(join(certDir, 'localhost.key')) },
      proxy: {
        '/api': {
          target: 'https://localhost:7080',
          changeOrigin: true,
          secure: true,
          ca: cert?.toString('utf8'),
        },
      },
    },
  };
});
