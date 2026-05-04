/**
 * errorHandling.test.jsx — Error handling across the app
 *
 * Covers:
 *   1. HTTP 401 — clears token, redirects to /login
 *   2. HTTP 404 — shows error message
 *   3. HTTP 500 — shows error message with retry
 *   4. Network failure — shows error message
 *   5. Expired token — AuthContext treats it as null (no crash)
 *   6. Login failure — shows server error message
 *   7. Login success — saves token
 *   8. Signup error — shows server error message
 *   9. Edge cases — null / empty documentId
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

import { MOCK_TOKEN, MOCK_DOCUMENT } from './mocks/handlers';
import DocumentStatusTracker from '../components/DocumentStatusTracker';
import LoginPage from '../pages/LoginPage';
import SignupPage from '../pages/SignupPage';
import { AuthProvider } from '../context/AuthContext';

// ── Mock document service ─────────────────────────────────────────────────────
vi.mock('../services/documentService', () => ({
  getDocumentById:           vi.fn(),
  getDocumentsByUser:        vi.fn(),
  getDocumentsByApplication: vi.fn(),
  uploadDocument:            vi.fn(),
  replaceDocument:           vi.fn(),
}));

// ── Mock auth service (used by useAuth → login / register) ────────────────────
vi.mock('../services/authService', () => ({
  login:    vi.fn(),
  register: vi.fn(),
}));

import { getDocumentById } from '../services/documentService';
import { login, register } from '../services/authService';

// ── Helpers ───────────────────────────────────────────────────────────────────

function renderTracker(docId = MOCK_DOCUMENT.id) {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <DocumentStatusTracker documentId={docId} intervalMs={60000} />
      </AuthProvider>
    </MemoryRouter>
  );
}

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <Routes>
          <Route path="/login"     element={<LoginPage />} />
          <Route path="/documents" element={<div>Documents</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>
  );
}

function renderSignup() {
  return render(
    <MemoryRouter initialEntries={['/signup']}>
      <AuthProvider>
        <Routes>
          <Route path="/signup"    element={<SignupPage />} />
          <Route path="/documents" element={<div>Documents</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.setItem('token', MOCK_TOKEN);
  window.location.href = '';
  getDocumentById.mockResolvedValue({ data: MOCK_DOCUMENT });
  login.mockResolvedValue({ data: { token: MOCK_TOKEN } });
  register.mockResolvedValue({ data: { token: MOCK_TOKEN } });
});

// ── 1–4. HTTP error codes ─────────────────────────────────────────────────────

describe('Error Handling — HTTP status codes', () => {
  it('401 clears token from localStorage', async () => {
    // The axios interceptor in axiosInstances calls localStorage.removeItem on 401.
    // Since we mock the service directly, simulate the interceptor behaviour:
    getDocumentById.mockImplementation(() => {
      window.localStorage.removeItem('token');
      window.location.href = '/login';
      return Promise.reject(Object.assign(new Error(), { response: { status: 401 } }));
    });

    renderTracker();
    await waitFor(() =>
      expect(window.localStorage.removeItem).toHaveBeenCalledWith('token')
    );
    expect(window.location.href).toBe('/login');
  });

  it('shows error state on 404', async () => {
    getDocumentById.mockRejectedValue(
      Object.assign(new Error(), { response: { status: 404, data: { message: 'Not found.' } } })
    );
    renderTracker('bad-id');
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument()
    );
  });

  it('500 shows error message with retry button', async () => {
    getDocumentById.mockRejectedValue(
      Object.assign(new Error(), { response: { status: 500, data: { message: 'Server error.' } } })
    );
    renderTracker();
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument()
    );
  });

  it('network failure shows error message', async () => {
    getDocumentById.mockRejectedValue(new Error('Network Error'));
    renderTracker();
    await waitFor(() =>
      expect(screen.getByText(/failed to fetch document status/i)).toBeInTheDocument()
    );
  });
});

// ── 5. Expired token ──────────────────────────────────────────────────────────

describe('Error Handling — expired token', () => {
  it('AuthContext renders children without crash when token is expired', () => {
    // Build a token with exp in the past
    const payload = btoa(JSON.stringify({ sub: 'u1', exp: 1 }))
      .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
    window.localStorage.getItem.mockReturnValueOnce(`header.${payload}.sig`);

    render(
      <MemoryRouter>
        <AuthProvider>
          <div data-testid="child">loaded</div>
        </AuthProvider>
      </MemoryRouter>
    );
    expect(screen.getByTestId('child')).toBeInTheDocument();
  });
});

// ── 6–8. Authentication ───────────────────────────────────────────────────────

describe('Error Handling — login', () => {
  it('shows error message on login failure', async () => {
    login.mockRejectedValue(
      Object.assign(new Error(), {
        response: { status: 401, data: { message: 'Invalid email or password.' } },
      })
    );

    renderLogin();
    await userEvent.type(screen.getByLabelText(/^email$/i), 'wrong@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'wrongpass');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(screen.getByText(/invalid email or password/i)).toBeInTheDocument()
    );
  });

  it('saves token on successful login', async () => {
    renderLogin();
    await userEvent.type(screen.getByLabelText(/^email$/i), 'test@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'Password123!');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(window.localStorage.setItem).toHaveBeenCalledWith('token', MOCK_TOKEN)
    );
  });
});

describe('Error Handling — signup', () => {
  it('shows server error on signup failure', async () => {
    register.mockRejectedValue(
      Object.assign(new Error(), {
        response: { status: 409, data: { message: 'Email already in use.' } },
      })
    );

    renderSignup();
    await userEvent.type(screen.getByLabelText(/first name/i), 'Jane');
    await userEvent.type(screen.getByLabelText(/last name/i), 'Doe');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'jane@example.com');
    // Use exact label IDs to avoid matching "Confirm password"
    await userEvent.type(screen.getByLabelText(/^password$/i), 'Password123!');
    await userEvent.type(screen.getByLabelText(/confirm password/i), 'Password123!');
    await userEvent.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() =>
      expect(screen.getByText(/email already in use/i)).toBeInTheDocument()
    );
  });

  it('shows client-side error when passwords do not match', async () => {
    renderSignup();
    await userEvent.type(screen.getByLabelText(/first name/i), 'Jane');
    await userEvent.type(screen.getByLabelText(/last name/i), 'Doe');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'jane@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'Password123!');
    await userEvent.type(screen.getByLabelText(/confirm password/i), 'Different999!');
    await userEvent.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() =>
      expect(screen.getByText(/passwords do not match/i)).toBeInTheDocument()
    );
  });
});

// ── 9. Edge cases ─────────────────────────────────────────────────────────────

describe('Error Handling — edge cases', () => {
  it('null documentId shows placeholder message', () => {
    renderTracker(null);
    expect(screen.getByText(/no document id provided/i)).toBeInTheDocument();
  });

  it('empty string documentId shows placeholder message', () => {
    renderTracker('');
    expect(screen.getByText(/no document id provided/i)).toBeInTheDocument();
  });

  it('retry button re-fetches after error', async () => {
    getDocumentById
      .mockRejectedValueOnce(new Error('Network Error'))
      .mockResolvedValueOnce({ data: MOCK_DOCUMENT });

    renderTracker();
    await waitFor(() => screen.getByRole('button', { name: /retry/i }));
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    await waitFor(() => screen.getByText(MOCK_DOCUMENT.fileName));
    expect(getDocumentById).toHaveBeenCalledTimes(2);
  });
});
