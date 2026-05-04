import { http, HttpResponse } from 'msw';

const DOC_BASE  = 'http://localhost:5023';
const AUTH_BASE = 'http://localhost:5021';

// ── Shared fixtures ───────────────────────────────────────────────────────────

export const MOCK_TOKEN =
  // header.payload.sig — payload: { sub, email, exp: year 2099 }
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.' +
  btoa(JSON.stringify({ sub: 'user-001', email: 'test@example.com', role: 'Applicant', exp: 4102444800 }))
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_') +
  '.signature';

export const MOCK_DOCUMENT = {
  id:            'doc-abc-123',
  applicationId: 'app-001',
  userId:        'user-001',
  fileName:      'passport.pdf',
  contentType:   'application/pdf',
  fileSizeBytes: 145678,
  documentType:  'NationalId',
  status:        'Pending',
  isVerified:    false,
  failureReason: null,
  createdAtUtc:  '2026-04-04T10:00:00Z',
  updatedAtUtc:  '2026-04-04T10:00:00Z',
};

// ── Default handlers (happy path) ─────────────────────────────────────────────

export const handlers = [

  // POST /api/auth/login
  http.post(`${AUTH_BASE}/api/auth/login`, () =>
    HttpResponse.json({ token: MOCK_TOKEN, expiresAt: '2099-01-01T00:00:00Z' })
  ),

  // POST /api/auth/register
  http.post(`${AUTH_BASE}/api/auth/register`, () =>
    HttpResponse.json({ token: MOCK_TOKEN, expiresAt: '2099-01-01T00:00:00Z' })
  ),

  // POST /api/documents/upload  → 200 Pending
  http.post(`${DOC_BASE}/api/documents/upload`, () =>
    HttpResponse.json(MOCK_DOCUMENT)
  ),

  // GET /api/documents/:id  → returns MOCK_DOCUMENT
  http.get(`${DOC_BASE}/api/documents/:id`, ({ params }) =>
    HttpResponse.json({ ...MOCK_DOCUMENT, id: params.id })
  ),

  // GET /api/documents/application/:id
  http.get(`${DOC_BASE}/api/documents/application/:id`, () =>
    HttpResponse.json([MOCK_DOCUMENT])
  ),

  // GET /api/documents/user/:id
  http.get(`${DOC_BASE}/api/documents/user/:id`, () =>
    HttpResponse.json([MOCK_DOCUMENT])
  ),

  // GET /health
  http.get(`${DOC_BASE}/health`, () =>
    HttpResponse.json({ status: 'Healthy', checks: [] })
  ),
];
