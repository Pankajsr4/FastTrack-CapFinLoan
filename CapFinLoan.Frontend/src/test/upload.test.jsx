// upload.test.jsx - Document upload flow tests
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { MOCK_TOKEN, MOCK_DOCUMENT } from './mocks/handlers';
import DocumentUploadPage from '../pages/DocumentUploadPage';
import { AuthProvider } from '../context/AuthContext';

vi.mock('../services/documentService', () => ({
  uploadDocument: vi.fn(), getDocumentById: vi.fn(),
  getDocumentsByUser: vi.fn(), getDocumentsByApplication: vi.fn(), replaceDocument: vi.fn(),
}));

import { uploadDocument } from '../services/documentService';

function renderPage() {
  return render(<MemoryRouter initialEntries={['/documents/upload']}><AuthProvider><DocumentUploadPage /></AuthProvider></MemoryRouter>);
}

const makePdf = (n = 'passport.pdf', s = 10000) => new File([new Uint8Array(s)], n, { type: 'application/pdf' });
const makeJpg = () => new File([new Uint8Array(5000)], 'photo.jpg', { type: 'image/jpeg' });
const makeExe = () => new File([new Uint8Array(1000)], 'virus.exe', { type: 'application/x-msdownload' });
const makeHuge = () => new File([new Uint8Array(6 * 1024 * 1024)], 'huge.pdf', { type: 'application/pdf' });

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.setItem('token', MOCK_TOKEN);
  uploadDocument.mockResolvedValue({ data: MOCK_DOCUMENT });
});

describe('Upload happy path', () => {
  it('renders form fields', () => {
    renderPage();
    expect(screen.getByLabelText(/application id/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/document type/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /upload document/i })).toBeInTheDocument();
  });
  it('submit disabled until applicationId and file provided', async () => {
    renderPage();
    const btn = screen.getByRole('button', { name: /upload document/i });
    expect(btn).toBeDisabled();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    expect(btn).toBeDisabled();
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf());
    expect(btn).not.toBeDisabled();
  });
  it('shows success card with document ID', async () => {
    renderPage();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf());
    await userEvent.click(screen.getByRole('button', { name: /upload document/i }));
    await waitFor(() => expect(screen.getByRole('heading', { name: /upload successful/i })).toBeInTheDocument());
    expect(screen.getByText(MOCK_DOCUMENT.id)).toBeInTheDocument();
  });
  it('accepts JPG files', async () => {
    renderPage();
    await userEvent.upload(document.querySelector('input[type="file"]'), makeJpg());
    expect(screen.getByText('photo.jpg')).toBeInTheDocument();
  });
  it('shows file name and size', async () => {
    renderPage();
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf('my-passport.pdf', 145678));
    expect(screen.getByText('my-passport.pdf')).toBeInTheDocument();
    expect(screen.getByText(/142\.3 KB/)).toBeInTheDocument();
  });
  it('Upload another resets form', async () => {
    renderPage();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf());
    await userEvent.click(screen.getByRole('button', { name: /upload document/i }));
    await waitFor(() => screen.getByRole('heading', { name: /upload successful/i }));
    await userEvent.click(screen.getByRole('button', { name: /upload another/i }));
    expect(screen.getByRole('button', { name: /upload document/i })).toBeInTheDocument();
  });
});

describe('Upload args', () => {
  it('calls uploadDocument with correct args', async () => {
    renderPage();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf());
    await userEvent.click(screen.getByRole('button', { name: /upload document/i }));
    await waitFor(() => expect(uploadDocument).toHaveBeenCalledTimes(1));
    const [appId, docType, file] = uploadDocument.mock.calls[0];
    expect(appId).toBe('app-001');
    expect(docType).toBe('NationalId');
    expect(file.name).toBe('passport.pdf');
  });
});

describe('Upload validation', () => {
  it('rejects exe via applyAccept false', async () => {
    const user = userEvent.setup({ applyAccept: false });
    renderPage();
    await user.upload(document.querySelector('input[type="file"]'), makeExe());
    expect(screen.getByText(/only pdf, jpg, and png/i)).toBeInTheDocument();
    expect(screen.queryByText('virus.exe')).not.toBeInTheDocument();
  });
  it('rejects files larger than 5 MB', async () => {
    renderPage();
    await userEvent.upload(document.querySelector('input[type="file"]'), makeHuge());
    expect(screen.getByText(/5 mb or smaller/i)).toBeInTheDocument();
  });
  it('submit stays disabled without file', async () => {
    renderPage();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    expect(screen.getByRole('button', { name: /upload document/i })).toBeDisabled();
  });
});

describe('Upload errors', () => {
  it('shows error card on 400', async () => {
    uploadDocument.mockRejectedValue(Object.assign(new Error(), { response: { status: 400, data: { message: 'File type is not supported.' } } }));
    renderPage();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf());
    await userEvent.click(screen.getByRole('button', { name: /upload document/i }));
    await waitFor(() => expect(screen.getByRole('heading', { name: /upload failed/i })).toBeInTheDocument());
    expect(screen.getByText(/file type is not supported/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /retry upload/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /start over/i })).toBeInTheDocument();
  });
  it('shows error card on 500', async () => {
    uploadDocument.mockRejectedValue(Object.assign(new Error(), { response: { status: 500, data: { message: 'Server error.' } } }));
    renderPage();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf());
    await userEvent.click(screen.getByRole('button', { name: /upload document/i }));
    await waitFor(() => expect(screen.getByRole('heading', { name: /upload failed/i })).toBeInTheDocument());
  });
  it('shows error card on network failure', async () => {
    uploadDocument.mockRejectedValue(new Error('Network Error'));
    renderPage();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf());
    await userEvent.click(screen.getByRole('button', { name: /upload document/i }));
    await waitFor(() => expect(screen.getByRole('heading', { name: /upload failed/i })).toBeInTheDocument());
  });
});

describe('Upload retry', () => {
  it('retry succeeds on second attempt', async () => {
    uploadDocument.mockRejectedValueOnce(Object.assign(new Error(), { response: { status: 503, data: { message: 'Temp error.' } } })).mockResolvedValueOnce({ data: MOCK_DOCUMENT });
    renderPage();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf());
    await userEvent.click(screen.getByRole('button', { name: /upload document/i }));
    await waitFor(() => screen.getByRole('button', { name: /retry upload/i }));
    await userEvent.click(screen.getByRole('button', { name: /retry upload/i }));
    await waitFor(() => expect(screen.getByRole('heading', { name: /upload successful/i })).toBeInTheDocument(), { timeout: 5000 });
    expect(uploadDocument).toHaveBeenCalledTimes(2);
  });
});

describe('Upload reset', () => {
  it('Start over clears error', async () => {
    uploadDocument.mockRejectedValue(new Error('Error'));
    renderPage();
    await userEvent.type(screen.getByLabelText(/application id/i), 'app-001');
    await userEvent.upload(document.querySelector('input[type="file"]'), makePdf());
    await userEvent.click(screen.getByRole('button', { name: /upload document/i }));
    await waitFor(() => screen.getByRole('button', { name: /start over/i }));
    await userEvent.click(screen.getByRole('button', { name: /start over/i }));
    expect(screen.getByRole('button', { name: /upload document/i })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /upload failed/i })).not.toBeInTheDocument();
  });
});
