import { defineConfig, devices } from '@playwright/test'

const port = 4173

// End-to-end tests drive the real frontend in a browser. The API is answered inside the
// browser by a fake (e2e/fake-api.ts), so the tests need no backend, database or model
// and give the same result every time.
export default defineConfig({
  testDir: './e2e',
  testMatch: '*.e2e.ts',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: `http://localhost:${port}`,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `npm run dev -- --port ${port} --strictPort`,
    url: `http://localhost:${port}`,
    reuseExistingServer: !process.env.CI,
  },
})
