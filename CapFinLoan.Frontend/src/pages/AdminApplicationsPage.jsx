import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { getAdminApplications, syncApplications } from '../services/adminService';
import { Search, RefreshCw } from 'lucide-react';

const STATUS_COLOUR = {
  Draft:           'bg-gray-100 text-gray-600',
  Submitted:       'bg-blue-100 text-blue-700',
  'Docs Pending':  'bg-yellow-100 text-yellow-700',
  'Docs Verified': 'bg-teal-100 text-teal-700',
  'Under Review':  'bg-purple-100 text-purple-700',
  Approved:        'bg-green-100 text-green-700',
  Rejected:        'bg-red-100 text-red-700',
  Disbursed:       'bg-violet-100 text-violet-700',
  Withdrawn:       'bg-orange-100 text-orange-700',
};

const FILTERS = ['All', 'Submitted', 'Docs Pending', 'Docs Verified', 'Under Review', 'Approved', 'Rejected', 'Disbursed'];

export default function AdminApplicationsPage() {
  const [apps,    setApps]    = useState([]);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState(null);
  const [filter,  setFilter]  = useState('All');
  const [search,  setSearch]  = useState('');
  const [syncing, setSyncing] = useState(false);
  const [syncMsg, setSyncMsg] = useState(null);

  const load = () => {
    setLoading(true);
    getAdminApplications()
      .then(r => setApps(r.data ?? []))
      .catch(() => setError('Failed to load applications.'))
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, []);

  const handleSync = async () => {
    setSyncing(true);
    setSyncMsg(null);
    try {
      const { data } = await syncApplications();
      setSyncMsg(`✅ Sync done — ${data.inserted} inserted, ${data.updated} updated (${data.total} total)`);
      load(); // reload the list
    } catch (e) {
      setSyncMsg(`❌ Sync failed: ${e?.response?.data?.message ?? e.message}`);
    } finally {
      setSyncing(false);
    }
  };

  const counts = apps.reduce((acc, a) => {
    acc[a.status] = (acc[a.status] ?? 0) + 1;
    return acc;
  }, {});

  const filtered = apps
    .filter(a => filter === 'All' || a.status === filter)
    .filter(a => {
      if (!search.trim()) return true;
      const q = search.toLowerCase();
      const name = (
        a.personalDetails
          ? `${a.personalDetails.firstName ?? ''} ${a.personalDetails.lastName ?? ''}`
          : `${a.firstName ?? ''} ${a.lastName ?? ''}`
      ).toLowerCase();
      return (
        name.includes(q) ||
        (a.email ?? a.personalDetails?.email ?? '').toLowerCase().includes(q) ||
        (a.applicationNumber ?? '').toLowerCase().includes(q)
      );
    });

  return (
    <div className="max-w-7xl mx-auto px-4 py-8 space-y-6">

      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Applications Queue</h1>
          <p className="text-sm text-gray-500 mt-0.5">{apps.length} total applications</p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={handleSync}
            disabled={syncing}
            className="flex items-center gap-1.5 text-xs bg-[#1e3a5f] hover:bg-[#0f2744] text-white px-3 py-1.5 rounded-lg transition-colors disabled:opacity-50"
            title="Pull all submitted applications from ApplicationService into admin DB"
          >
            <RefreshCw size={13} className={syncing ? 'animate-spin' : ''} />
            {syncing ? 'Syncing…' : 'Sync from Source'}
          </button>
          <Link to="/admin/dashboard" className="text-xs text-blue-600 hover:underline font-medium">
            ← Back to Dashboard
          </Link>
        </div>
      </div>

      {/* Sync result message */}
      {syncMsg && (
        <div className={`text-sm rounded-lg px-4 py-3 ${
          syncMsg.startsWith('✅')
            ? 'bg-green-50 border border-green-200 text-green-800'
            : 'bg-red-50 border border-red-200 text-red-700'
        }`}>
          {syncMsg}
        </div>
      )}

      {/* Search */}
      <div className="relative max-w-sm">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
        <input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Search by name, email or app #"
          className="w-full pl-9 pr-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      {/* Filter tabs */}
      <div className="flex gap-2 flex-wrap">
        {FILTERS.map(s => (
          <button key={s} onClick={() => setFilter(s)}
            className={`text-xs px-3 py-1.5 rounded-full font-medium transition-colors
              ${filter === s
                ? 'bg-[#1e3a5f] text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}>
            {s}{s !== 'All' && counts[s] ? ` (${counts[s]})` : ''}
          </button>
        ))}
      </div>

      {/* Error */}
      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3">
          {error}
        </div>
      )}

      {/* Table */}
      {loading ? (
        <div className="space-y-2">
          {[...Array(6)].map((_, i) => (
            <div key={i} className="h-14 bg-gray-100 rounded-xl animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {['Application #', 'Applicant', 'Amount', 'Tenure', 'Status', 'Submitted', 'Action'].map(h => (
                  <th key={h} className="text-left text-xs font-semibold text-gray-500 uppercase tracking-wide px-4 py-3">
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.length === 0 && (
                <tr>
                  <td colSpan={7} className="text-center text-gray-400 py-10 text-sm">
                    No applications found
                  </td>
                </tr>
              )}
              {filtered.map(app => {
                const name = app.personalDetails
                  ? `${app.personalDetails.firstName ?? ''} ${app.personalDetails.lastName ?? ''}`.trim()
                  : `${app.firstName ?? ''} ${app.lastName ?? ''}`.trim() || app.applicantName || '—';
                const email = app.personalDetails?.email ?? app.email ?? '';
                const amount = app.loanDetails?.requestedAmount ?? app.requestedAmount ?? 0;
                const tenure = app.loanDetails?.requestedTenureMonths ?? app.requestedTenureMonths ?? '—';

                return (
                  <tr key={app.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3 font-mono text-xs text-gray-700">
                      {app.applicationNumber}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1.5">
                        <div>
                          <p className="font-medium text-gray-900 text-xs">{name}</p>
                          <p className="text-[11px] text-gray-400">{email}</p>
                        </div>
                        {app.isEdited && (
                          <span className="text-[10px] bg-orange-100 text-orange-700 px-1.5 py-0.5 rounded-full font-semibold shrink-0">
                            Edited ×{app.editCount}
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-xs text-gray-700">
                      ₹{Number(amount).toLocaleString()}
                    </td>
                    <td className="px-4 py-3 text-xs text-gray-500">
                      {tenure}m
                    </td>
                    <td className="px-4 py-3">
                      <span className={`text-[11px] font-semibold px-2 py-0.5 rounded-full ${STATUS_COLOUR[app.status] ?? 'bg-gray-100 text-gray-600'}`}>
                        {app.status}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-xs text-gray-400">
                      {app.submittedAtUtc ? new Date(app.submittedAtUtc).toLocaleDateString() : '—'}
                    </td>
                    <td className="px-4 py-3">
                      <Link to={`/admin/applications/${app.id}`}
                        className="text-xs text-blue-600 hover:underline font-medium">
                        Review →
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
