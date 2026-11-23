import { defineConfig, devices } from '@playwright/test'

const isCI = !!process.env.CI

// End-to-end tests run against the real app: the ASP.NET Core API plus the
// Vite dev server, which proxies /api to the API. The API runs in Demo mode,
// so results are deterministic and no model is needed.
export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  reporter: isCI ? [['html', { open: 'never' }], ['list']] : 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  // Chromium only in CI; locally also Firefox and WebKit.
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    ...(isCI
      ? []
      : [
          { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
          { name: 'webkit', use: { ...devices['Desktop Safari'] } },
        ]),
  ],
  webServer: [
    {
      command: 'dotnet run --project ../src/LegacyLens.Api --launch-profile http',
      url: 'http://localhost:5080/api/health',
      // A separate database so e2e runs do not fill the developer's History. CI sets its own.
      env: {
        Llm__Provider: 'Demo',
        ConnectionStrings__LegacyLens:
          process.env.ConnectionStrings__LegacyLens ??
          'Server=(localdb)\\MSSQLLocalDB;Database=LegacyLens_E2E;Trusted_Connection=True;TrustServerCertificate=True',
      },
      reuseExistingServer: !isCI,
      timeout: 180_000,
    },
    {
      command: 'npm run dev -- --port 5173 --strictPort',
      url: 'http://localhost:5173',
      reuseExistingServer: !isCI,
    },
  ],
})
