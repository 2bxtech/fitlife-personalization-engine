import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests run against a real API with demo mode enabled (see
 * .github/workflows/deploy.yml, job e2e). Locally: start the Kafka-free stack or
 * `dotnet run --project FitLife.Api`, then `npm run build && npm run preview`
 * (preview proxies /api to the API on :5269) and `npm run test:e2e`.
 */
export default defineConfig({
  testDir: './e2e',
  // Tests share synthetic personas, and a session resets its persona; run serially.
  workers: 1,
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:4173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile', use: { ...devices['Pixel 7'] } },
  ],
})
