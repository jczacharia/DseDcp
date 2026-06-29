import {expect, test} from './fixtures';

test('app renders title', async ({page}) => {
  await page.goto('/');
  await expect(page).toHaveTitle('Enterprise Search');
});
