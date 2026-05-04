/**
 * login.test.jsx — Login page unit tests
 *
 * Covers:
 *   1. Renders email, password fields and submit button
 *   2. Successful login → stores token, redirects applicant to /applicant/dashboard
 *   3. Successful admin login → redirects to /admin/dashboard
 *   4. Server 401 → shows error message
 *   5. Server 500 → shows generic error
 *   6. Network failure → shows error
 *   7. Loading state disables button during request
 *   8. Sign up link is present
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import LoginPage from '../pages/LoginPage';
import { AuthProvider } from '../context/AuthContext';

// ── Mock authService ──────────────────────────────────────────────────────────
vi.mock('../services/authService', () => ({
  login:    vi.fn(),
  register: vi.fn(),
}));

import { login } from '../services/authService';

// Helpers to get inputs by placeholder
const emailInput    = () => screen.getByPlaceholderText(/you@example\.com/i);
const passwordInput = () => screen.getByPlaceholderText(/••••••••/);

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <LoginPage />
      </AuthProvider>
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.clear();
  window.location.href = '';
  login.mockResolvedValue({ data: { token: 'test.jwt.token', role: 'Applicant' } });
});

// ── 1. Renders correctly ──────────────────────────────────────────────────────

describe('LoginPage — rendering', () => {
  it('renders email input', () => {
    renderLogin();
    expect(emailInput()).toBeInTheDocument();
  });

  it('renders password input', () => {
    renderLogin();
    expect(passwordInput()).toBeInTheDocument();
  });

  it('renders sign in button', () => {
    renderLogin();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
  });

  it('renders sign up link', () => {
    renderLogin();
    expect(screen.getByRole('link', { name: /create one/i })).toBeInTheDocument();
  });

  it('renders CapFinLoan branding', () => {
    renderLogin();
    expect(screen.getByText(/capfinloan/i)).toBeInTheDocument();
  });
});

// ── 2. Successful login ───────────────────────────────────────────────────────

describe('LoginPage — successful login', () => {
  it('stores token in localStorage on success', async () => {
    login.mockResolvedValue({ data: { token: 'test.jwt.token', role: 'Applicant' } });

    renderLogin();
    await userEvent.type(emailInput(), 'user@example.com');
    await userEvent.type(passwordInput(), 'Password1!');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(window.localStorage.setItem).toHaveBeenCalledWith('token', 'test.jwt.token')
    );
  });

  it('redirects applicant to /applicant/dashboard', async () => {
    login.mockResolvedValue({ data: { token: 'test.jwt.token', role: 'Applicant' } });

    renderLogin();
    await userEvent.type(emailInput(), 'user@example.com');
    await userEvent.type(passwordInput(), 'Password1!');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(window.location.href).toBe('/applicant/dashboard')
    );
  });

  it('redirects admin to /admin/dashboard', async () => {
    login.mockResolvedValue({ data: { token: 'admin.jwt.token', role: 'Admin' } });

    renderLogin();
    await userEvent.type(emailInput(), 'admin@example.com');
    await userEvent.type(passwordInput(), 'AdminPass1!');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(window.location.href).toBe('/admin/dashboard')
    );
  });

  it('stores role in localStorage', async () => {
    login.mockResolvedValue({ data: { token: 'test.jwt.token', role: 'Applicant' } });

    renderLogin();
    await userEvent.type(emailInput(), 'user@example.com');
    await userEvent.type(passwordInput(), 'Password1!');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(window.localStorage.setItem).toHaveBeenCalledWith('role', 'APPLICANT')
    );
  });
});

// ── 3. Error states ───────────────────────────────────────────────────────────

describe('LoginPage — error handling', () => {
  it('shows error message on 401', async () => {
    login.mockRejectedValue(
      Object.assign(new Error(), { response: { status: 401, data: { message: 'Invalid email or password.' } } })
    );

    renderLogin();
    await userEvent.type(emailInput(), 'wrong@example.com');
    await userEvent.type(passwordInput(), 'wrongpass');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(screen.getByText(/invalid email or password/i)).toBeInTheDocument()
    );
  });

  it('shows generic error on 500', async () => {
    login.mockRejectedValue(
      Object.assign(new Error(), { response: { status: 500, data: { message: 'Internal server error.' } } })
    );

    renderLogin();
    await userEvent.type(emailInput(), 'user@example.com');
    await userEvent.type(passwordInput(), 'Password1!');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(screen.getByText(/internal server error/i)).toBeInTheDocument()
    );
  });

  it('shows error on network failure', async () => {
    login.mockRejectedValue(new Error('Network Error'));

    renderLogin();
    await userEvent.type(emailInput(), 'user@example.com');
    await userEvent.type(passwordInput(), 'Password1!');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(screen.getByText(/network error/i)).toBeInTheDocument()
    );
  });

  it('shows error when response has no token', async () => {
    login.mockResolvedValue({ data: { message: 'ok but no token' } });

    renderLogin();
    await userEvent.type(emailInput(), 'user@example.com');
    await userEvent.type(passwordInput(), 'Password1!');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() =>
      expect(screen.getByText(/no token/i)).toBeInTheDocument()
    );
  });
});

// ── 4. Loading state ──────────────────────────────────────────────────────────

describe('LoginPage — loading state', () => {
  it('disables button while request is in flight', async () => {
    // Never resolves — keeps the button in loading state
    login.mockReturnValue(new Promise(() => {}));

    renderLogin();
    await userEvent.type(emailInput(), 'user@example.com');
    await userEvent.type(passwordInput(), 'Password1!');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    expect(screen.getByRole('button', { name: /signing in/i })).toBeDisabled();
  });
});
