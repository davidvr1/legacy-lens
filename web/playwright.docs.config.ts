import { defineConfig, devices } from '@playwright/test'
import base from './playwright.config.ts'

// Generates the README screenshots and GIF (npm run screenshots). Same servers as the e2e tests
// (API in Demo mode + Vite), Chromium only, and not part of the normal test run.
export default defineConfig({
  ...base,
  testDir: './docs',
  fullyParallel: false,
  retries: 0,
  reporter: 'list',
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
