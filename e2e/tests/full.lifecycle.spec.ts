import { test, expect } from '@playwright/test';
import { createHmac } from 'crypto';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';

const API     = process.env['API_BASE_URL']     ?? 'http://localhost:5170';
const DOCS    = process.env['DOCS_BASE_URL']    ?? 'http://localhost:56163';
const PROFILE = process.env['PROFILE_BASE_URL'] ?? 'http://localhost:56161';
const APP     = process.env['APP_BASE_URL']     ?? 'http://localhost:4200';
const DEV_KEY = 'dev-signing-key-32-bytes-minimum!';

function makeJwt(tenantId = '00000000-0000-0000-0000-000000000001'): string {
  const header  = Buffer.from(JSON.stringify({ alg: 'HS256', typ: 'JWT' })).toString('base64url');
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

/** Upload a file fully through Storage API and mark it ready. Returns fileId. */
async function uploadAndMarkReady(
  request: Parameters<Parameters<typeof test>[1]>[0]['request'],
  opts: { category: string; fileName: string; mimeType: string; content: Buffer },
): Promise<string> {
  const key = require('crypto').randomUUID().replace(/-/g, '');
  const checksum = require('crypto').createHash('sha256').update(opts.content).digest('hex');

  const init = await request.post(`${API}/v1/files`, {
    headers: { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key },
    data: { categoryId: opts.category, originalFileName: opts.fileName, mimeType: opts.mimeType, sizeBytes: opts.content.length, idempotencyKey: key, ownerService: 'e2e-test' },
  });
  expect(init.status()).toBe(200);
  const { fileId } = await init.json() as { fileId: string };

  const put = await request.put(`${API}/v1/files/${fileId}`, { headers: { ...auth(), 'Content-Type': opts.mimeType }, data: opts.content });
  expect(put.status()).toBe(204);

  const complete = await request.post(`${API}/v1/files/${fileId}/complete`, { headers: { ...auth(), 'Content-Type': 'application/json' }, data: { checksumSha256: checksum, sizeBytes: opts.content.length } });
  expect(complete.status()).toBe(200);

  const ready = await request.post(`${API}/v1/dev/files/${fileId}/mark-ready`, { headers: auth() });
  expect(ready.status()).toBe(204);

  return fileId;
}

// ─── API: Full File Lifecycle ─────────────────────────────────────────────────

test.describe('API — Full file lifecycle', () => {
  let fileId: string;
  const idempotencyKey = require('crypto').randomUUID().replace(/-/g, '');
  const fileContent = Buffer.from('E2E test file content for lifecycle test');
  const checksum = require('crypto').createHash('sha256').update(fileContent).digest('hex');

  test('L01 — initiate upload returns fileId and proxyRequired', async ({ request }) => {
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey },
      data: { categoryId: 'document', originalFileName: 'e2e-test.pdf', mimeType: 'application/pdf', sizeBytes: fileContent.length, idempotencyKey, ownerService: 'e2e-test' },
    });
    expect(resp.status()).toBe(200);
    const body = await resp.json() as { fileId: string; proxyRequired: boolean };
    expect(body.fileId).toBeTruthy();
    expect(body.proxyRequired).toBe(true);
    fileId = body.fileId;
  });

  test('L02 — proxy upload stores file bytes (204)', async ({ request }) => {
    expect(fileId, 'L01 must run first').toBeTruthy();
    const resp = await request.put(`${API}/v1/files/${fileId}`, { headers: { ...auth(), 'Content-Type': 'application/pdf' }, data: fileContent });
    expect(resp.status()).toBe(204);
  });

  test('L03 — complete upload transitions file to scanning', async ({ request }) => {
    expect(fileId, 'L02 must run first').toBeTruthy();
    const resp = await request.post(`${API}/v1/files/${fileId}/complete`, {
      headers: { ...auth(), 'Content-Type': 'application/json' },
      data: { checksumSha256: checksum, sizeBytes: fileContent.length },
    });
    expect(resp.status()).toBe(200);
    expect((await resp.json() as { status: string }).status).toBe('scanning');
  });

  test('L04 — list files shows the uploaded file', async ({ request }) => {
    expect(fileId, 'L03 must run first').toBeTruthy();
    const resp = await request.get(`${API}/v1/files?categoryId=document`, { headers: auth() });
    expect(resp.status()).toBe(200);
    const body = await resp.json() as { items: Array<{ fileId: string; status: string }> };
    const found = body.items.find(f => f.fileId === fileId);
    expect(found).toBeTruthy();
    expect(found!.status).toBe('scanning');
  });

  test('L05 — mark-ready transitions file to ready', async ({ request }) => {
    expect(fileId, 'L04 must run first').toBeTruthy();
    expect((await request.post(`${API}/v1/dev/files/${fileId}/mark-ready`, { headers: auth() })).status()).toBe(204);
  });

  test('L06 — download file content returns bytes', async ({ request }) => {
    expect(fileId, 'L05 must run first').toBeTruthy();
    const resp = await request.get(`${API}/v1/files/${fileId}/content`, { headers: auth() });
    expect(resp.status()).toBe(200);
    const body = await resp.body();
    expect(body.length).toBeGreaterThan(0);
    expect(body.toString()).toBe(fileContent.toString());
  });

  test('L07 — get file metadata shows ready status', async ({ request }) => {
    expect(fileId, 'L05 must run first').toBeTruthy();
    const resp = await request.get(`${API}/v1/files/${fileId}`, { headers: auth() });
    expect(resp.status()).toBe(200);
    const body = await resp.json() as { status: string; originalFileName: string };
    expect(body.status).toBe('ready');
    expect(body.originalFileName).toBe('e2e-test.pdf');
  });

  test('L08 — delete file returns 204', async ({ request }) => {
    expect(fileId, 'L07 must run first').toBeTruthy();
    expect((await request.delete(`${API}/v1/files/${fileId}`, { headers: auth() })).status()).toBe(204);
  });

  test('L09 — get deleted file returns 404', async ({ request }) => {
    expect(fileId, 'L08 must run first').toBeTruthy();
    expect((await request.get(`${API}/v1/files/${fileId}`, { headers: auth() })).status()).toBe(404);
  });
});

// ─── API: Idempotency ─────────────────────────────────────────────────────────

test.describe('API — Idempotency', () => {
  test('I01 — same idempotency key returns same fileId', async ({ request }) => {
    const key = require('crypto').randomUUID().replace(/-/g, '');
    const body = { categoryId: 'document', originalFileName: 'idem-test.pdf', mimeType: 'application/pdf', sizeBytes: 512, idempotencyKey: key, ownerService: 'e2e-test' };
    const headers = { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key };
    const r1 = await request.post(`${API}/v1/files`, { headers, data: body });
    const r2 = await request.post(`${API}/v1/files`, { headers, data: body });
    expect(r1.status()).toBe(200);
    expect(r2.status()).toBe(200);
    expect((await r1.json() as { fileId: string }).fileId).toBe((await r2.json() as { fileId: string }).fileId);
  });

  test('I02 — same key different payload returns 409', async ({ request }) => {
    const key = require('crypto').randomUUID().replace(/-/g, '');
    const headers = { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key };
    const body1 = { categoryId: 'document', originalFileName: 'file1.pdf', mimeType: 'application/pdf', sizeBytes: 100, idempotencyKey: key, ownerService: 'e2e-test' };
    expect((await request.post(`${API}/v1/files`, { headers, data: body1 })).status()).toBe(200);
    expect((await request.post(`${API}/v1/files`, { headers, data: { ...body1, sizeBytes: 999 } })).status()).toBe(409);
  });
});

// ─── API: Validation ──────────────────────────────────────────────────────────

test.describe('API — Validation', () => {
  test('V01 — oversized file returns 413', async ({ request }) => {
    const key = require('crypto').randomUUID().replace(/-/g, '');
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key },
      data: { categoryId: 'document', originalFileName: 'huge.pdf', mimeType: 'application/pdf', sizeBytes: 200 * 1024 * 1024, idempotencyKey: key, ownerService: 'e2e-test' },
    });
    expect(resp.status()).toBe(413);
  });

  test('V02 — disallowed mime type returns 415', async ({ request }) => {
    const key = require('crypto').randomUUID().replace(/-/g, '');
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key },
      data: { categoryId: 'image', originalFileName: 'script.exe', mimeType: 'application/octet-stream', sizeBytes: 1024, idempotencyKey: key, ownerService: 'e2e-test' },
    });
    expect(resp.status()).toBe(415);
  });

  test('V03 — disallowed file extension returns 415', async ({ request }) => {
    const key = require('crypto').randomUUID().replace(/-/g, '');
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...auth(), 'Content-Type': 'application/json', 'Idempotency-Key': key },
      data: { categoryId: 'document', originalFileName: 'malware.exe', mimeType: 'application/pdf', sizeBytes: 1024, idempotencyKey: key, ownerService: 'e2e-test' },
    });
    expect(resp.status()).toBe(415);
  });
});

// ─── Documents Service API ────────────────────────────────────────────────────

test.describe('Documents Service API', () => {
  let docId: string;

  test('D01 — initiate, proxy-upload, and complete a document upload', async ({ request }) => {
    const content = Buffer.from('D01 document content');
    const checksum = require('crypto').createHash('sha256').update(content).digest('hex');

    // Step 1: initiate via Documents service (server calls Storage SDK internally)
    const initResp = await request.post(`${DOCS}/api/documents`, {
      headers: { ...auth(), 'Content-Type': 'application/json' },
      data: { fileName: 'd01-test.pdf', mimeType: 'application/pdf', sizeBytes: content.length },
    });
    expect(initResp.status()).toBe(200);
    const init = await initResp.json() as { id: string; fileId: string; proxyRequired: boolean; proxyUploadUrl: string; completeUrl: string };
    expect(init.id).toBeTruthy();
    expect(init.proxyRequired).toBe(true);
    docId = init.id;

    // Step 2: proxy upload bytes through Documents service
    const uploadResp = await request.put(`${DOCS}${init.proxyUploadUrl}`, {
      headers: { ...auth(), 'Content-Type': 'application/pdf' },
      data: content,
    });
    expect(uploadResp.status()).toBe(204);

    // Step 3: complete — Documents service calls Storage SDK + marks ready in dev
    const completeResp = await request.post(`${DOCS}${init.completeUrl}`, {
      headers: { ...auth(), 'Content-Type': 'application/json' },
      data: { checksumSha256: checksum, sizeBytes: content.length },
    });
    expect(completeResp.status()).toBe(200);
    const doc = await completeResp.json() as { id: string; fileId: string; status: string };
    expect(doc.id).toBe(docId);
    expect(doc.status).toBe('ready');
  });

  test('D02 — list documents includes created record', async ({ request }) => {
    expect(docId, 'D01 must run first').toBeTruthy();
    const resp = await request.get(`${DOCS}/api/documents`, { headers: auth() });
    expect(resp.status()).toBe(200);
    const docs = await resp.json() as Array<{ id: string }>;
    expect(docs.some(d => d.id === docId)).toBe(true);
  });

  test('D03 — delete document returns 204 and removes from list', async ({ request }) => {
    expect(docId, 'D02 must run first').toBeTruthy();
    expect((await request.delete(`${DOCS}/api/documents/${docId}`, { headers: auth() })).status()).toBe(204);
    const docs = await (await request.get(`${DOCS}/api/documents`, { headers: auth() })).json() as Array<{ id: string }>;
    expect(docs.some(d => d.id === docId)).toBe(false);
  });
});

// ─── Profile Service API ──────────────────────────────────────────────────────

test.describe('Profile Service API', () => {
  test('P01 — get profile returns 200 with userId', async ({ request }) => {
    const resp = await request.get(`${PROFILE}/api/profiles/me`, { headers: auth() });
    expect(resp.status()).toBe(200);
    expect((await resp.json() as { userId: string }).userId).toBeTruthy();
  });

  test('P02 — initiate, proxy-upload, and complete avatar upload', async ({ request }) => {
    const content = Buffer.from('fake-avatar-bytes');
    const checksum = require('crypto').createHash('sha256').update(content).digest('hex');

    // Step 1: initiate via Profile service
    const initResp = await request.post(`${PROFILE}/api/profiles/me/avatar`, {
      headers: { ...auth(), 'Content-Type': 'application/json' },
      data: { fileName: 'avatar.jpg', mimeType: 'image/jpeg', sizeBytes: content.length },
    });
    expect(initResp.status()).toBe(200);
    const init = await initResp.json() as { fileId: string; proxyRequired: boolean; proxyUploadUrl: string; completeUrl: string };
    expect(init.fileId).toBeTruthy();
    expect(init.proxyRequired).toBe(true);

    // Step 2: proxy upload through Profile service
    const uploadResp = await request.put(`${PROFILE}${init.proxyUploadUrl}`, {
      headers: { ...auth(), 'Content-Type': 'image/jpeg' },
      data: content,
    });
    expect(uploadResp.status()).toBe(204);

    // Step 3: complete — Profile service marks ready in dev
    const completeResp = await request.post(`${PROFILE}${init.completeUrl}`, {
      headers: { ...auth(), 'Content-Type': 'application/json' },
      data: { checksumSha256: checksum, sizeBytes: content.length },
    });
    expect(completeResp.status()).toBe(200);
    const profile = await completeResp.json() as { avatarFileId: string; avatarStatus: string };
    expect(profile.avatarFileId).toBe(init.fileId);
    expect(profile.avatarStatus).toBe('ready');
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
    const tmpFile = path.join(os.tmpdir(), 'e2e-upload.pdf');
    fs.writeFileSync(tmpFile, '%PDF-1.4 E2E test document content');

    await page.locator('input[type="file"]').setInputFiles(tmpFile);
    await expect(page.locator('text=Uploaded:')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.file-table tbody tr').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.badge-ready').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('button.btn-download').first()).toBeVisible();
    await expect(page.locator('button.btn-delete').first()).toBeVisible();

    fs.unlinkSync(tmpFile);
  });

  test('UI03 — delete removes file from list', async ({ page }) => {
    await page.goto(`${APP}/documents`);
    await expect(page.locator('.file-table, .empty').first()).toBeVisible({ timeout: 8000 });

    const deleteBtn = page.locator('button.btn-delete').first();
    if (await deleteBtn.isVisible()) {
      const rowsBefore = await page.locator('.file-table tbody tr').count();
      await deleteBtn.click();
      await expect(page.locator('.file-table tbody tr')).toHaveCount(rowsBefore - 1, { timeout: 5000 });
    } else {
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
    const jpegBytes = Buffer.from(
      '/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkS' +
      'Ew8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJ' +
      'CQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIy' +
      'MjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAACf/' +
      'EABQQAQAAAAAAAAAAAAAAAAAAAAD/xAAUAQEAAAAAAAAAAAAAAAAAAAAA/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/' +
      'aAAwDAQACEQMRAD8AJQAB/9k=', 'base64');
    fs.writeFileSync(tmpFile, jpegBytes);

    await page.locator('input[type="file"]').setInputFiles(tmpFile);
    await expect(page.locator('text=Uploaded:')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.avatar-img')).toBeVisible({ timeout: 15000 });

    fs.unlinkSync(tmpFile);
  });
});
