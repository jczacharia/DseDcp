import {defineConfig, devices} from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
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
    command: 'dotnet run --project srv/Dse.Api/Dse.Api.csproj --launch-profile http',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
    stdout: 'ignore',
    stderr: 'pipe',
  },
});
