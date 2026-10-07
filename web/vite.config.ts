/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const server = 'http://localhost:7341'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    include: ['src/**/*.test.ts'],
  },
  build: {
    // The OVSD host serves the built client as static files.
    outDir: '../server/src/OVSD.Host/wwwroot',
    emptyOutDir: true,
  },
  server: {
    host: true,
    proxy: {
      '/api': server,
      '/media': server,
      '/ws': { target: server, ws: true },
    },
  },
})
