import '@testing-library/jest-dom';
import { afterEach, beforeAll, afterAll, vi } from 'vitest';
import { cleanup } from '@testing-library/react';
import { server } from './mocks/server';

// ── MSW ───────────────────────────────────────────────────────────────────────
beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }));
afterEach(() => { server.resetHandlers(); cleanup(); });
afterAll(() => server.close());

// ── Env vars ──────────────────────────────────────────────────────────────────
vi.stubEnv('VITE_SIGNALR_HUB_URL',     'http://localhost:5023/hubs/document-status');
vi.stubEnv('VITE_DOCUMENT_API_URL',    'http://localhost:5023');
vi.stubEnv('VITE_AUTH_API_URL',        'http://localhost:5021');
vi.stubEnv('VITE_APPLICATION_API_URL', 'http://localhost:5022');

// ── SignalR mock ──────────────────────────────────────────────────────────────
// Exported so individual tests can call mockConnection.emit(event, payload)
const _handlers = {};

export const mockConnection = {
  start:          vi.fn().mockResolvedValue(undefined),
  stop:           vi.fn().mockResolvedValue(undefined),
  invoke:         vi.fn().mockResolvedValue(undefined),
  on:             vi.fn((event, cb) => { _handlers[event] = cb; }),
  onclose:        vi.fn(),
  onreconnecting: vi.fn(),
  onreconnected:  vi.fn(),
  state:          'Connected',
  /** Fire a hub event from a test */
  emit: (event, payload) => _handlers[event]?.(payload),
};

vi.mock('@microsoft/signalr', () => {
  const builder = {
    withUrl:                vi.fn().mockReturnThis(),
    withAutomaticReconnect: vi.fn().mockReturnThis(),
    configureLogging:       vi.fn().mockReturnThis(),
    build:                  vi.fn(() => mockConnection),
  };
  function HubConnectionBuilder() { return builder; }
  return {
    HubConnectionBuilder,
    HttpTransportType:  { WebSockets: 1, ServerSentEvents: 2, LongPolling: 4 },
    HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' },
    LogLevel:           { Warning: 1 },
  };
});

// ── localStorage stub ─────────────────────────────────────────────────────────
const _store = {};
Object.defineProperty(window, 'localStorage', {
  value: {
    getItem:    vi.fn((k) => _store[k] ?? null),
    setItem:    vi.fn((k, v) => { _store[k] = v; }),
    removeItem: vi.fn((k) => { delete _store[k]; }),
    clear:      vi.fn(() => { Object.keys(_store).forEach((k) => delete _store[k]); }),
  },
  writable: true,
});

// ── window.location stub ──────────────────────────────────────────────────────
delete window.location;
window.location = { href: '', pathname: '/dashboard', assign: vi.fn() };
