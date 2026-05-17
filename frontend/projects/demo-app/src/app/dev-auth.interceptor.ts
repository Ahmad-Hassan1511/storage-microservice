import { HttpInterceptorFn } from '@angular/common/http';

// Dev JWT signed with DevelopmentSigningKey = "dev-signing-key-32-bytes-minimum!" (HS256).
// The API has ValidateLifetime=false and ValidateIssuer=false in dev mode, so this never expires.
const DEV_TOKEN =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9' +
  '.eyJzdWIiOiJkZXYtdXNlciIsInRpZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMSIsInNjcCI6ImZpbGVzLnJlYWQgZmlsZXMud3JpdGUifQ' +
  '.-lD-oM9nDd5y5EXijhtkTOk1VrzXifq-rkFxG9F-j_E';

export const devAuthInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.url.startsWith('/v1') || req.url.startsWith('/api')) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${DEV_TOKEN}` } });
  }
  return next(req);
};
