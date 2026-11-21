import { expect, test } from '@playwright/test'

test('home page loads', async ({ page }) => {
  await page.goto('/')

  await expect(page).toHaveTitle('LegacyLens')
  await expect(page.getByTestId('app-title')).toHaveText('LegacyLens')
})
