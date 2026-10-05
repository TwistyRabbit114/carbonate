/// <reference types="vitest/config" />
import { rmSync } from 'node:fs';
import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig, type Plugin } from 'vite';

//the msw worker lives in public/ so dev mode can register it, but it should never ship
function dropMockWorker(): Plugin {
  return {
    name: 'drop-mock-worker',
    apply: 'build',
    closeBundle() {
      rmSync(fileURLToPath(new URL('./dist/mockServiceWorker.js', import.meta.url)), { force: true });
    },
  };
}

export default defineConfig({
  plugins: [react(), dropMockWorker()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    //same-origin in dev too, so the refresh cookie behaves like it will in production.
    //point this at the https port in the api's launchSettings.json
    proxy: {
      '/api': { target: 'https://localhost:7001', secure: false },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: { modules: { classNameStrategy: 'non-scoped' } },
  },
});
