import { useState, useEffect, useCallback } from 'react';
import { getDocumentsByApplication } from '../services/documentService';

/**
 * Fetches and manages documents for a given applicationId.
 * Re-fetches whenever applicationId changes or refresh() is called.
 */
export function useDocuments(applicationId) {
  const [documents, setDocuments] = useState([]);
  const [loading, setLoading]     = useState(false);
  const [error, setError]         = useState(null);

  const fetch = useCallback(async () => {
    if (!applicationId) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await getDocumentsByApplication(applicationId);
      setDocuments(data);
    } catch (err) {
      setError(err.response?.data?.message ?? err.message);
    } finally {
      setLoading(false);
    }
  }, [applicationId]);

  useEffect(() => { fetch(); }, [fetch]);

  return { documents, loading, error, refresh: fetch };
}
