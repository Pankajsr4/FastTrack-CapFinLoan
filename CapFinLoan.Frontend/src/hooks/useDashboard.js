import { useState, useEffect, useCallback } from 'react';
import { getDocumentsByUser } from '../services/documentService';

/**
 * Fetches all documents for the given userId and computes dashboard statistics.
 *
 * Returns:
 *   stats        — { total, pending, processing, completed, failed, underReview, verified }
 *   recent       — last 5 documents sorted by createdAtUtc desc
 *   loading      — true on first fetch
 *   error        — error message or null
 *   refresh()    — manually re-fetch
 */
export function useDashboard(userId) {
  const [documents, setDocuments] = useState([]);
  const [loading,   setLoading]   = useState(false);
  const [error,     setError]     = useState(null);

  const fetch = useCallback(async () => {
    if (!userId) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await getDocumentsByUser(userId);
      setDocuments(data ?? []);
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to load dashboard data.');
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => { fetch(); }, [fetch]);

  // ── Compute stats ─────────────────────────────────────────────────────────
  const stats = {
    total:       documents.length,
    pending:     documents.filter((d) => d.status === 'Pending').length,
    processing:  documents.filter((d) => d.status === 'Processing').length,
    completed:   documents.filter((d) => d.status === 'Completed').length,
    underReview: documents.filter((d) => d.status === 'UnderReview').length,
    verified:    documents.filter((d) => d.status === 'Verified').length,
    failed:      documents.filter((d) => d.status === 'Failed' || d.status === 'ReuploadRequired').length,
  };

  const recent = [...documents]
    .sort((a, b) => new Date(b.createdAtUtc) - new Date(a.createdAtUtc))
    .slice(0, 5);

  return { stats, recent, loading, error, refresh: fetch };
}
