import { defineConfig, devices } from '@playwright/test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const clientDir = path.dirname(fileURLToPath(import.meta.url))

export default defineConfig({
  testDir: 'e2e',
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  globalSetup: path.join(clientDir, 'e2e', 'global-setup.integration.js'),
  globalTeardown: path.join(clientDir, 'e2e', 'global-teardown.integration.js'),
  use: {
    ...devices['Desktop Chrome'],
    baseURL: 'http://127.0.0.1:4173',
  },
  projects: [
    {
      name: 'integration',
      testMatch: 'pit-inference.spec.js',
    },
  ],
})
