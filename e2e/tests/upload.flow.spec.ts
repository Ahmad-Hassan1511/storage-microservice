import { test, expect } from '@playwright/test';

const APP_URL = process.env['APP_BASE_URL'] ?? 'http://localhost:4200';

// ─── Browser UI E2E flows ─────────────────────────────────────────────────────

test.describe('Document upload flow', () => {
  test('E-UI01 — documents page loads and shows upload button', async ({ page }) => {
    await page.goto(`${APP_URL}/documents`);
    await expect(page.locator('h2')).toContainText('Documents');
    await expect(page.locator('lib-file-uploader button')).toBeVisible();
  });

  test('E-UI02 — profile page loads and shows avatar upload', async ({ page }) => {
    await page.goto(`${APP_URL}/profile`);
    await expect(page.locator('h2')).toContainText('Profile');
    await expect(page.locator('lib-file-uploader button')).toBeVisible();
  });
});
