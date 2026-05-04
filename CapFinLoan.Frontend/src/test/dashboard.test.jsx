/**
 * dashboard.test.jsx — Applicant Dashboard page tests
 *
 * Covers:
 *   1. Shows welcome message with user name
 *   2. Renders quick action links (New Application, Upload)
 *   3. Shows stat cards after data loads
 *   4. Renders recent applications list
 *   5. Shows "No applications" when list is empty
 *   6. Shows recent documents
 *   7. Handles API errors gracefully (no crash)
 *   8. AiChatWidget floating button is present
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import DashboardPage from '../pages/DashboardPage';
import { AuthProvider } from '../context/AuthContext';
import { MOCK_TOKEN } from './mocks/handlers';

// ── Mock services ─────────────────────────────────────────────────────────────
vi.mock('../services/applicationService', () => ({
  getMyApplications: vi.fn(),
  withdrawApplication: vi.fn(),
}));

vi.mock('../services/documentService', () => ({
  getDocumentsByUser:        vi.fn(),
  getDocumentsByApplication: vi.fn(),
  getDocumentById:           vi.fn(),
  uploadDocument:            vi.fn(),
  replaceDocument:           vi.fn(),
}));

// AiChatWidget uses SignalR — already mocked in setup.js
// Mock the AI service so it doesn't make real HTTP calls
vi.mock('../services/aiService', () => ({
  sendChatMessage: vi.fn().mockResolvedValue({ data: { reply: 'Hello!' } }),
}));

import { getMyApplications } from '../services/applicationService';
import { getDocumentsByUser } from '../services/documentService';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const MOCK_APP = {
  id:                'app-001',
  applicationNumber: 'APP-20260429-1234',
  status:            'Submitted',
  createdAtUtc:      '2026-04-01T10:00:00Z',
  loanDetails:       { requestedAmount: 500000, requestedTenureMonths: 60, loanPurpose: 'Personal Loan' },
  personalDetails:   { firstName: 'Arjun', lastName: 'Sharma', email: 'arjun@example.com' },
};

const MOCK_DOC = {
  id:           'doc-001',
  documentType: 'NationalId',
  fileName:     'aadhaar.pdf',
  status:       'Verified',
};

// ── Helpers ───────────────────────────────────────────────────────────────────

function renderDashboard(tokenOverride = MOCK_TOKEN) {
  window.localStorage.setItem('token', tokenOverride);
  return render(
    <MemoryRouter>
      <AuthProvider>
        <DashboardPage />
      </AuthProvider>
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.setItem('token', MOCK_TOKEN);
  getMyApplications.mockResolvedValue({ data: [MOCK_APP] });
  getDocumentsByUser.mockResolvedValue({ data: [MOCK_DOC] });
});

// ── 1. Welcome message ────────────────────────────────────────────────────────

describe('DashboardPage — welcome', () => {
  it('renders welcome heading', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByText(/welcome back/i)).toBeInTheDocument()
    );
  });
});

// ── 2. Quick actions ──────────────────────────────────────────────────────────

describe('DashboardPage — quick actions', () => {
  it('renders New Application link', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByRole('link', { name: /new application/i })).toBeInTheDocument()
    );
  });

  it('New Application link points to /applications/new', async () => {
    renderDashboard();
    await waitFor(() => {
      const link = screen.getByRole('link', { name: /new application/i });
      expect(link).toHaveAttribute('href', '/applications/new');
    });
  });

  it('renders Upload Document link', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByRole('link', { name: /upload document/i })).toBeInTheDocument()
    );
  });
});

// ── 3. Stat cards ─────────────────────────────────────────────────────────────

describe('DashboardPage — stat cards', () => {
  it('shows Applications count', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByText('Applications')).toBeInTheDocument()
    );
  });

  it('shows Documents count', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByText('Documents')).toBeInTheDocument()
    );
  });

  it('shows Verified count', async () => {
    renderDashboard();
    await waitFor(() =>
      // "Verified" appears as both a stat label and a document status badge
      expect(screen.getAllByText('Verified').length).toBeGreaterThanOrEqual(1)
    );
  });
});

// ── 4. Recent applications ────────────────────────────────────────────────────

describe('DashboardPage — recent applications', () => {
  it('renders application number', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByText('APP-20260429-1234')).toBeInTheDocument()
    );
  });

  it('renders application status badge', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByText('Submitted')).toBeInTheDocument()
    );
  });

  it('renders View all link', async () => {
    renderDashboard();
    await waitFor(() =>
      // Dashboard has two "View all" links (applications + documents)
      expect(screen.getAllByRole('link', { name: /view all/i }).length).toBeGreaterThanOrEqual(1)
    );
  });
});

// ── 5. Empty state ────────────────────────────────────────────────────────────

describe('DashboardPage — empty state', () => {
  it('shows no applications message when list is empty', async () => {
    getMyApplications.mockResolvedValue({ data: [] });
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByText(/no applications yet/i)).toBeInTheDocument()
    );
  });

  it('shows start application link when empty', async () => {
    getMyApplications.mockResolvedValue({ data: [] });
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByRole('link', { name: /start one/i })).toBeInTheDocument()
    );
  });
});

// ── 6. Recent documents ───────────────────────────────────────────────────────

describe('DashboardPage — recent documents', () => {
  it('renders document type', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByText('NationalId')).toBeInTheDocument()
    );
  });

  it('renders document file name', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByText('aadhaar.pdf')).toBeInTheDocument()
    );
  });
});

// ── 7. Error handling ─────────────────────────────────────────────────────────

describe('DashboardPage — error handling', () => {
  it('does not crash when applications API fails', async () => {
    getMyApplications.mockRejectedValue(new Error('Network Error'));
    expect(() => renderDashboard()).not.toThrow();
    // Page should still render the welcome heading
    await waitFor(() =>
      expect(screen.getByText(/welcome back/i)).toBeInTheDocument()
    );
  });

  it('does not crash when documents API fails', async () => {
    getDocumentsByUser.mockRejectedValue(new Error('Network Error'));
    expect(() => renderDashboard()).not.toThrow();
    await waitFor(() =>
      expect(screen.getByText(/welcome back/i)).toBeInTheDocument()
    );
  });
});

// ── 8. AI Chat Widget ─────────────────────────────────────────────────────────

describe('DashboardPage — AI chat widget', () => {
  it('renders the AI assistant floating button', async () => {
    renderDashboard();
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /ai assistant/i })).toBeInTheDocument()
    );
  });
});
