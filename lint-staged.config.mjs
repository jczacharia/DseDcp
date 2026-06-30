import {relative} from 'node:path';

/**
 * Root-orchestrated lint-staged.
 * @type {import('lint-staged').Configuration}
 */
export default {
  '*.{cs,csproj,esproj,props,targets}': (files) => [
    'dotnet tool restore',
    `dotnet csharpier format ${files.map((file) => relative(process.cwd(), file)).join(' ')}`,
  ],
  'ui/**/*.{ts,js,html}': ['eslint --fix'],
  '*.{ts,js,mjs,mts,html,json,css,scss,md,svg,yml,yaml}': ['prettier --write'],
};
