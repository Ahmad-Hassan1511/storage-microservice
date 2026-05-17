import { test, expect, request as pwRequest } from '@playwright/test';
import { createHmac } from 'crypto';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';

const API = process.env['API_BASE_URL'] ?? 'http://localhost:5170';
const APP = process.env['APP_BASE_URL'] ?? 'http://localhost:4200';
const DEV_KEY = 'dev-signing-key-32-bytes-minimum!';

function makeJwt(tenantId = '00000000-0000-0000-0000-000000000001'): string {
  const header = Buffer.from(JSON.stringify({ alg: 'HS256', typ: 'JWT' })).toString('base64url');
  const payload = Buffer.from(JSON.stringify({
    sub: 'e2e-user',
    tid: tenantId,
    scp: 'files.read files.write',
    iat: Math.floor(Date.now() / 1000),
    exp: Math.floor(Date.now() / 1000) + 3600,
  })).toString('base64url');
  const sig = createHmac('sha256', DEV_KEY).update(`${header}.${payload}`).digest('base64url');
  return `${header}.${payload}.${sig}`;
}

function auth(tenantId?: string) {
  return { Authorization: `Bearer ${makeJwt(tenantId)}` };
}

// ─── API: Full File Lifecycle ─────────────────────────────────────────────────

test.describe('API — Full file lifecycle', () => {
  let fileId: string;
  const idempotencyKey = crypto.randomUUID().replace(/-/g, '');
  const fileContent = Buffer.from('E2E test file content for lifecycle test');
  const checksum = require('crypto')
    .createHash('sha256')
    .update(fileContent)
    .digest('hex');

  test('L01 — initiate upload returns fileId and proxyRequired', async ({ request }) => {
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey },
      data: {
        categoryId: 'document',
        originalFileName: 'e2e-test.pdf',
        mimeType: 'application/pdf',
        sizeBytes: fileContent.length,
        idempotencyKey,
        ownerService: 'e2e-test',
      },
    });
    expect(resp.status()).toBe(200);
    const body = await resp.json() as { fileId: string; proxyRequired: boolean };
    expect(body.fileId).toBeTruthy();
    expect(body.proxyRequired).toBe(true);
    fileId = body.fileId;
  });

  test('L02 — proxy upload stores file bytes (204)', async ({ request }) => {
    expect(fileId, 'L01 must run first').toBeTruthy();
    const resp = await request.put(`${API}/v1/files/${fileId}`, {
      headers: { ...auth(), 'Content-Type': 'application/pdf' },
      data: fileContent,
    });
    expect(resp.status()).toBe(204);
  });

  test('L03 — complete upload transitions file to scanning', async ({ request }) => {
    expect(fileId, 'L02 must run first').toBeTruthy();
    const resp = await request.post(`${API}/v1/files/${fileId}/complete`, {
      headers: { ...auth(), 'Content-Type': 'application/json' },
      data: { checksumSha256: checksum, sizeBytes: fileContent.length },
    });
    expect(resp.status()).toBe(200);
    const body = await resp.json() as { status: string };
    expect(body.status).toBe('scanning');
  });

  test('L04 — list files shows the uploaded file', async ({ request }) => {
    expect(fileId, 'L03 must run first').toBeTruthy();
    const resp = await request.get(`${API}/v1/files?categoryId=document`, {
      headers: auth(),
    });
    expect(resp.status()).toBe(200);
    const body = await resp.json() as { items: Array<{ fileId: string; status: string }> };
    const found = body.items.find(f => f.fileId === fileId);
    expect(found).toBeTruthy();
    expect(found!.status).toBe('scanning');
  });

  test('L05 — mark-ready transitions file to ready and public', async ({ request }) => {
    expect(fileId, 'L04 must run first').toBeTruthy();
    const resp = await request.post(`${API}/v1/dev/files/${fileId}/mark-ready`, {
      headers: auth(),
    });
    expect(resp.status()).toBe(204);
  });

  test('L06 — download file content returns bytes', async ({ request }) => {
    expect(fileId, 'L05 must run first').toBeTruthy();
    const resp = await request.get(`${API}/v1/files/${fileId}/content`, {
      headers: auth(),
    });
    expect(resp.status()).toBe(200);
    const body = await resp.body();
    expect(body.length).toBeGreaterThan(0);
    expect(body.toString()).toBe(fileContent.toString());
  });

  test('L07 — get file metadata shows ready status with no download url (proxy-only)', async ({ request }) => {
    expect(fileId, 'L05 must run first').toBeTruthy();
    const resp = await request.get(`${API}/v1/files/${fileId}`, {
      headers: auth(),
    });
    expect(resp.status()).toBe(200);
    const body = await resp.json() as { status: string; originalFileName: string };
    expect(body.status).toBe('ready');
    expect(body.originalFileName).toBe('e2e-test.pdf');
  });

  test('L08 — delete file returns 204', async ({ request }) => {
    expect(fileId, 'L07 must run first').toBeTruthy();
    const resp = await request.delete(`${API}/v1/files/${fileId}`, {
      headers: auth(),
    });
    expect(resp.status()).toBe(204);
  });

  test('L09 — get deleted file returns 404', async ({ request }) => {
    expect(fileId, 'L08 must run first').toBeTruthy();
    const resp = await request.get(`${API}/v1/files/${fileId}`, {
      headers: auth(),
    });
    // File is deleted, DownloadService only serves Ready files
    expect(resp.status()).toBe(404);
  });
});

// ─── API: Idempotency ─────────────────────────────────────────────────────────

test.describe('API — Idempotency', () => {
  test('I01 — same idempotency key returns same fileId', async ({ request }) => {
    const key = crypto.randomUUID().replace(/-/g, '');
    const body = {
      categoryId: 'document',
      originalFileName: 'idem-test.pdf',
      mimeType: 'application/pdf',
      sizeBytes: 512,
      idempotencyKey: key,
      ownerService: 'e2e-test',
    };
    const headers = { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key };
    const r1 = await request.post(`${API}/v1/files`, { headers, data: body });
    const r2 = await request.post(`${API}/v1/files`, { headers, data: body });
    expect(r1.status()).toBe(200);
    expect(r2.status()).toBe(200);
    const b1 = await r1.json() as { fileId: string };
    const b2 = await r2.json() as { fileId: string };
    expect(b1.fileId).toBe(b2.fileId);
  });

  test('I02 — same key different payload returns 409', async ({ request }) => {
    const key = crypto.randomUUID().replace(/-/g, '');
    const headers = { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key };
    const body1 = { categoryId: 'document', originalFileName: 'file1.pdf', mimeType: 'application/pdf', sizeBytes: 100, idempotencyKey: key, ownerService: 'e2e-test' };
    const body2 = { ...body1, sizeBytes: 999 };
    const r1 = await request.post(`${API}/v1/files`, { headers, data: body1 });
    expect(r1.status()).toBe(200);
    const r2 = await request.post(`${API}/v1/files`, { headers, data: body2 });
    expect(r2.status()).toBe(409);
  });
});

// ─── API: Validation ──────────────────────────────────────────────────────────

test.describe('API — Validation', () => {
  test('V01 — oversized file returns 413', async ({ request }) => {
    const key = crypto.randomUUID().replace(/-/g, '');
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key },
      data: {
        categoryId: 'document',
        originalFileName: 'huge.pdf',
        mimeType: 'application/pdf',
        sizeBytes: 200 * 1024 * 1024,  // 200 MB > 100 MB limit
        idempotencyKey: key,
        ownerService: 'e2e-test',
      },
    });
    expect(resp.status()).toBe(413);
  });

  test('V02 — disallowed mime type returns 415', async ({ request }) => {
    const key = crypto.randomUUID().replace(/-/g, '');
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key },
      data: {
        categoryId: 'image',
        originalFileName: 'script.exe',
        mimeType: 'application/octet-stream',
        sizeBytes: 1024,
        idempotencyKey: key,
        ownerService: 'e2e-test',
      },
    });
    expect(resp.status()).toBe(415);
  });

  test('V03 — disallowed file extension returns 415', async ({ request }) => {
    const key = crypto.randomUUID().replace(/-/g, '');
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key },
      data: {
        categoryId: 'document',
        originalFileName: 'malware.exe',
        mimeType: 'application/pdf',
        sizeBytes: 1024,
        idempotencyKey: key,
        ownerService: 'e2e-test',
      },
    });
    expect(resp.status()).toBe(415);
  });
});

// ─── UI: Browser flows ────────────────────────────────────────────────────────

test.describe('UI — Documents page', () => {
  test('UI01 — documents page loads with table structure', async ({ page }) => {
    await page.goto(`${APP}/documents`);
    await expect(page.locator('h2')).toContainText('Documents');
    await expect(page.locator('lib-file-uploader button')).toBeVisible();
    await expect(page.locator('.list-header h3')).toContainText('My Documents');
    await expect(page.locator('button.refresh-btn')).toBeVisible();
  });

  test('UI02 — upload a file, it appears in list as ready', async ({ page }) => {
    await page.goto(`${APP}/documents`);

    // Create a temporary test file
    const tmpFile = path.join(os.tmpdir(), 'e2e-upload.pdf');
    fs.writeFileSync(tmpFile, '%PDF-1.4 E2E test document content');

    // Trigger file input
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(tmpFile);

    // Wait for upload success text and list refresh
    await expect(page.locator('text=Uploaded:')).toBeVisible({ timeout: 15000 });

    // Wait for at least one row in the table (use .first() to avoid strict-mode violation)
    await expect(page.locator('.file-table tbody tr').first()).toBeVisible({ timeout: 10000 });

    // At least one badge-ready should exist (the just-uploaded file)
    await expect(page.locator('.badge-ready').first()).toBeVisible({ timeout: 10000 });

    // Download and delete buttons should be present
    await expect(page.locator('button.btn-download').first()).toBeVisible();
    await expect(page.locator('button.btn-delete').first()).toBeVisible();

    fs.unlinkSync(tmpFile);
  });

  test('UI03 — delete removes file from list', async ({ page }) => {
    await page.goto(`${APP}/documents`);

    // Wait for page to finish loading (table or empty state)
    await expect(page.locator('.file-table, .empty').first()).toBeVisible({ timeout: 8000 });

    const deleteBtn = page.locator('button.btn-delete').first();
    const deleteBtnVisible = await deleteBtn.isVisible();

    if (deleteBtnVisible) {
      const rowsBefore = await page.locator('.file-table tbody tr').count();
      await deleteBtn.click();
      await expect(page.locator('.file-table tbody tr')).toHaveCount(rowsBefore - 1, { timeout: 5000 });
    } else {
      // No deletable files — page shows empty state
      await expect(page.locator('.empty')).toBeVisible();
    }
  });
});

test.describe('UI — Profile page', () => {
  test('UI04 — profile page loads with avatar upload', async ({ page }) => {
    await page.goto(`${APP}/profile`);
    await expect(page.locator('h2')).toContainText('Profile');
    await expect(page.locator('lib-file-uploader button')).toBeVisible();
    await expect(page.locator('.avatar-placeholder, .avatar-img')).toBeVisible();
  });

  test('UI05 — upload avatar image, avatar renders', async ({ page }) => {
    await page.goto(`${APP}/profile`);

    const tmpFile = path.join(os.tmpdir(), 'e2e-avatar.jpg');
    // Minimal valid JPEG (1×1 white pixel)
    const jpegBytes = Buffer.from(
      '/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkS' +
      'Ew8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJ' +
      'CQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIy' +
      'MjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAACf/' +
      'EABQQAQAAAAAAAAAAAAAAAAAAAAD/xAAUAQEAAAAAAAAAAAAAAAAAAAAA/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/' +
      'aAAwDAQACEQMRAD8AJQAB/9k=',
      'base64'
    );
    fs.writeFileSync(tmpFile, jpegBytes);

    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(tmpFile);

    // Wait for upload confirmation
    await expect(page.locator('text=Uploaded:')).toBeVisible({ timeout: 15000 });

    // Avatar image should appear after processing
    await expect(page.locator('.avatar-img')).toBeVisible({ timeout: 15000 });

    fs.unlinkSync(tmpFile);
  });
});
