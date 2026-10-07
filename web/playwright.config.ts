import { defineConfig } from '@playwright/test'

export const E2E_PORT = 7399

// End-to-end tests against a real OVSD host (fresh temporary data directory) using the installed Edge.
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: `http://localhost:${E2E_PORT}`,
    channel: 'msedge',
    trace: 'retain-on-failure',
  },
  webServer: {
    command: `node e2e/start-server.mjs ${E2E_PORT}`,
    url: `http://localhost:${E2E_PORT}/api/server`,
    timeout: 180_000,
    reuseExistingServer: false,
    stdout: 'ignore',
    stderr: 'pipe',
  },
})
