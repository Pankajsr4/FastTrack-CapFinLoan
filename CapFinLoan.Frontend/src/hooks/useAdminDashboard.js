import { useState, useEffect, useCallback } from 'react';
import { getDashboardAnalytics } from '../services/adminService';

export function useAdminDashboard() {
  const [data,          setData]          = useState(null);
  const [loading,       setLoading]       = useState(true);
  const [error,         setError]         = useState(null);
  const [lastFetchedAt, setLastFetchedAt] = useState(null); // client-side timestamp

  const fetch = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getDashboardAnalytics();
      setData(res.data);
      setLastFetchedAt(new Date()); // always reflects when WE fetched, not server cache time
    } catch (err) {
      setError(err?.response?.data?.message ?? 'Failed to load dashboard analytics.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetch(); }, [fetch]);

  return { data, loading, error, lastFetchedAt, refetch: fetch };
}
