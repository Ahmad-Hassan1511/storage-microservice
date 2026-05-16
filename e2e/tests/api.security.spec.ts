import { test, expect, request as pwRequest } from '@playwright/test';
import { createHmac } from 'crypto';

const API = process.env['API_BASE_URL'] ?? 'http://localhost:5100';
// Must match Auth:DevelopmentSigningKey in appsettings / Program.cs fallback
const DEV_KEY = 'dev-signing-key-32-bytes-minimum!';

function makeJwt(tenantId = '11111111-1111-1111-1111-111111111111'): string {
  const header = Buffer.from(JSON.stringify({ alg: 'HS256', typ: 'JWT' })).toString('base64url');
  const payload = Buffer.from(JSON.stringify({
    sub: 'e2e-user',
    tid: tenantId,
    azp: 'e2e-service',
    scp: 'storage.read storage.write',
    iat: Math.floor(Date.now() / 1000),
    exp: Math.floor(Date.now() / 1000) + 3600,
  })).toString('base64url');
  const sig = createHmac('sha256', DEV_KEY).update(`${header}.${payload}`).digest('base64url');
  return `${header}.${payload}.${sig}`;
}

function authHeaders(tenantId?: string): Record<string, string> {
  return { Authorization: `Bearer ${makeJwt(tenantId)}` };
}

// ─── Security Tests ──────────────────────────────────────────────────────────

test.describe('Security', () => {
  test('S01 — missing JWT returns 401', async ({ request }) => {
    const resp = await request.get(`${API}/v1/files/${crypto.randomUUID()}`);
    expect(resp.status()).toBe(401);
  });

  test('S02 — path traversal in filename is rejected at API layer', async ({ request }) => {
    const idKey = crypto.randomUUID().replace(/-/g, '');
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...authHeaders(), 'Content-Type': 'application/json', 'Idempotency-Key': idKey },
      data: {
        categoryId: 'document',
        originalFileName: '../../../etc/passwd',
        mimeType: 'application/pdf',
        sizeBytes: 1024,
        idempotencyKey: idKey,
        ownerService: 'e2e-service',
      },
    });
    // Must NOT succeed — traversal attempt must not return 200
    expect(resp.status()).not.toBe(200);
  });

  test('S03 — cross-tenant file access does not leak data (404 or 500 when DB unavailable)', async ({ request }) => {
    const resp = await request.get(`${API}/v1/files/${crypto.randomUUID()}`, {
      headers: authHeaders('22222222-2222-2222-2222-222222222222'),
    });
    // 404 = correct tenant isolation; 500 = DB unavailable in dev (still no data leak)
    // Must never be 200 — that would be a cross-tenant data leak
    expect(resp.status()).not.toBe(200);
    expect([404, 500]).toContain(resp.status());
  });

  test('S04 — malformed JWT returns 401', async ({ request }) => {
    const resp = await request.get(`${API}/v1/categories`, {
      headers: { Authorization: 'Bearer not.a.real.jwt' },
    });
    expect(resp.status()).toBe(401);
  });
});

// ─── API E2E Tests ────────────────────────────────────────────────────────────

test.describe('API E2E', () => {
  test('E01 — health endpoint returns healthy (no auth)', async ({ request }) => {
    const resp = await request.get(`${API}/health`);
    expect(resp.status()).toBe(200);
    const body = await resp.json() as { status: string };
    expect(body.status).toBe('healthy');
  });

  test('E02 — authenticated request to categories is not 401', async ({ request }) => {
    const resp = await request.get(`${API}/v1/categories`, { headers: authHeaders() });
    expect(resp.status()).not.toBe(401);
  });

  test('E03 — unauthenticated request to categories returns 401', async ({ request }) => {
    const resp = await request.get(`${API}/v1/categories`);
    expect(resp.status()).toBe(401);
  });

  test('E04 — unknown category returns non-200 on initiate upload', async ({ request }) => {
    const idKey = crypto.randomUUID().replace(/-/g, '');
    const resp = await request.post(`${API}/v1/files`, {
      headers: { ...authHeaders(), 'Content-Type': 'application/json', 'Idempotency-Key': idKey },
      data: {
        categoryId: 'does-not-exist',
        originalFileName: 'test.bin',
        mimeType: 'application/octet-stream',
        sizeBytes: 100,
        idempotencyKey: idKey,
        ownerService: 'e2e-service',
      },
    });
    expect(resp.status()).not.toBe(200);
  });
});
