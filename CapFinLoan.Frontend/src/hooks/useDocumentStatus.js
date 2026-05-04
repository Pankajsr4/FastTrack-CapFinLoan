import { useState, useEffect, useRef, useCallback } from 'react';
import { getDocumentById } from '../services/documentService';
import { useSignalR, CONNECTION_STATE } from './useSignalR';

const TERMINAL_STATUSES = new Set(['Completed', 'Failed', 'Verified', 'ReuploadRequired', 'UnderReview']);

/**
 * Tracks a document's status using SignalR (primary) with HTTP polling as fallback.
 *
 * Strategy:
 *   1. Connect to SignalR hub and subscribe to the document's group.
 *   2. When a "DocumentStatusUpdated" message arrives, update state immediately.
 *   3. If SignalR is Disconnected or Reconnecting, fall back to HTTP polling
 *      every `fallbackIntervalMs` so the UI never goes stale.
 *   4. Stop all updates once a terminal status is reached.
 *
 * @param {string|null} documentId
 * @param {number}      fallbackIntervalMs — polling interval when SignalR is down (default 5 s)
 */
export function useDocumentStatus(documentId, fallbackIntervalMs = 5000) {
  const [document,   setDocument]   = useState(null);
  const [loading,    setLoading]    = useState(false);
  const [error,      setError]      = useState(null);
  const [isTerminal, setIsTerminal] = useState(false);
  const [history,    setHistory]    = useState([]); // [{status, timestamp}]

  const mountedRef  = useRef(true);
  const timerRef    = useRef(null);
  const terminalRef = useRef(false);   // ref copy so callbacks don't capture stale state

  // ── Apply an update (from SignalR or HTTP) ────────────────────────────────
  const applyUpdate = useCallback((data) => {
    if (!mountedRef.current || terminalRef.current) return;
    setDocument(data);
    setError(null);

    // Append to history if status changed
    setHistory((prev) => {
      const last = prev[prev.length - 1];
      if (last?.status === data.status) return prev;
      return [...prev, { status: data.status, timestamp: new Date() }];
    });

    if (TERMINAL_STATUSES.has(data.status)) {
      terminalRef.current = true;
      setIsTerminal(true);
      clearInterval(timerRef.current);
    }
  }, []);

  // ── HTTP fetch (used for initial load + fallback polling) ─────────────────
  const fetchStatus = useCallback(async () => {
    if (!documentId || terminalRef.current) return;
    try {
      const { data } = await getDocumentById(documentId);
      if (mountedRef.current) applyUpdate(data);
    } catch (err) {
      if (mountedRef.current)
        setError(err.response?.data?.message ?? 'Failed to fetch document status.');
    } finally {
      if (mountedRef.current) setLoading(false);
    }
  }, [documentId, applyUpdate]);

  // ── SignalR — receives instant push updates ───────────────────────────────
  const handleSignalRUpdate = useCallback((update) => {
    // The hub sends { documentId, status, updatedAt, failureReason }
    // Merge into the existing document object so all fields stay populated
    setDocument((prev) => {
      if (!prev) return prev;
      const merged = { ...prev, status: update.status, updatedAt: update.updatedAt, failureReason: update.failureReason ?? prev.failureReason };
      applyUpdate(merged);
      return merged;
    });
  }, [applyUpdate]);

  const { connectionState } = useSignalR(documentId, handleSignalRUpdate);

  // ── Fallback polling — active when SignalR is not Connected ───────────────
  useEffect(() => {
    if (isTerminal) { clearInterval(timerRef.current); return; }

    const needsFallback = connectionState === CONNECTION_STATE.Disconnected
                       || connectionState === CONNECTION_STATE.Reconnecting;

    if (needsFallback) {
      timerRef.current = setInterval(fetchStatus, fallbackIntervalMs);
    } else {
      clearInterval(timerRef.current);
    }

    return () => clearInterval(timerRef.current);
  }, [connectionState, isTerminal, fetchStatus, fallbackIntervalMs]);

  // ── Initial load ──────────────────────────────────────────────────────────
  useEffect(() => {
    mountedRef.current  = true;
    terminalRef.current = false;

    if (!documentId) {
      setDocument(null);
      setLoading(false);
      setError(null);
      setIsTerminal(false);
      return;
    }

    setLoading(true);
    setIsTerminal(false);
    setHistory([]);
    fetchStatus();

    return () => {
      mountedRef.current = false;
      clearInterval(timerRef.current);
    };
  }, [documentId, fetchStatus]);

  return {
    document,
    status:          document?.status ?? null,
    loading,
    error,
    isTerminal,
    history,
    connectionState,
    refresh:         fetchStatus,
  };
}
