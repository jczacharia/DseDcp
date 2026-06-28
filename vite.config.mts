import angular from '@analogjs/vite-plugin-angular';
import {playwright} from '@vitest/browser-playwright';
import viteTsConfigPaths from 'vite-tsconfig-paths';
import {defineConfig} from 'vitest/config';

export default defineConfig(({mode}) => ({
  plugins: [angular({tsconfig: './tsconfig.spec.json'}), viteTsConfigPaths({projects: ['./tsconfig.spec.json']})],
  test: {
    globals: true,
    watch: false,
    pool: 'vmThreads',
    environment: 'jsdom',
    setupFiles: ['ui/testing/test-setup.ts'],
    include: ['ui/**/*.spec.ts'],
    reporters: ['default', ['vitest-sonar-reporter', {outputFile: 'coverage/ut_report.xml'}]],
    browser: {
      enabled: true,
      headless: mode === 'production',
      provider: playwright(),
      instances: [{browser: 'chromium'}],
    },
    coverage: {
      provider: 'v8',
      enabled: mode === 'production',
      include: ['ui/**/*.ts'],
      exclude: ['ui/api/**', 'ui/ui/**', 'ui/testing/**', 'ui/**/*.spec.ts', 'ui/**/*.d.ts'],
      reportsDirectory: 'coverage',
      reporter: ['text', 'lcovonly', ['cobertura', {file: 'test-coverage.xml'}]],
    },
  },
  define: {
    'import.meta.vitest': mode !== 'production',
  },
}));
