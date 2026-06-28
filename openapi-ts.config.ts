import {defineConfig} from '@hey-api/openapi-ts';

export default defineConfig({
  input: './srv/Dse.Api/Dse.Api.json',
  output: {
    path: './ui/api',
    tsConfigPath: './tsconfig.app.json',
    postProcess: ['prettier'],
  },
  plugins: [
    {name: '@hey-api/client-angular', throwOnError: true},
    {name: '@hey-api/schemas'},
    {name: '@hey-api/transformers'},
    {name: 'zod'},
    {name: '@hey-api/sdk', transformer: true},
    {name: '@hey-api/typescript'},
  ],
});
