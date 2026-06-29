import {expect, test} from './fixtures';

test('app renders title', async ({page}) => {
  await page.goto('/');
  await expect(page).toHaveTitle('Enterprise Search');
});

test('app forwards api health', async ({page}) => {
  const [response] = await Promise.all([
    page.waitForResponse((resp) => resp.url().includes('/api/health') && resp.status() === 200),
    page.goto('/api/health'),
  ]);
  const jsonBody = await response.json();
  expect(jsonBody).toMatchObject({status: 'healthy'});
});
