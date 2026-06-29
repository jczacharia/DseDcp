import {defineConfig, devices} from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  reporter: [['list']],
  tsconfig: './tsconfig.e2e.json',
  globalTeardown: './e2e/coverage-report.ts',
  use: {
    baseURL: 'http://localhost:4200',
    locale: 'en-US',
    colorScheme: 'dark',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
  },
  projects: [{name: 'chromium', use: {...devices['Desktop Chrome']}}],
  webServer: {
    command: 'npm run start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
    stdout: 'ignore',
    stderr: 'pipe',
  },
});
