import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { getMyApplications, withdrawApplication } from '../services/applicationService';
import { PlusCircle, ChevronRight, LogOut } from 'lucide-react';
import AiChatWidget from '../components/AiChatWidget';

const STATUS_COLOUR = {
  Draft:           'bg-gray-100 text-gray-600',
  Submitted:       'bg-blue-100 text-blue-700',
  'Docs Pending':  'bg-yellow-100 text-yellow-700',
  'Docs Verified': 'bg-teal-100 text-teal-700',
  'Under Review':  'bg-purple-100 text-purple-700',
  Approved:        'bg-green-100 text-green-700',
  Rejected:        'bg-red-100 text-red-700',
  Closed:          'bg-gray-100 text-gray-500',
  Withdrawn:       'bg-orange-100 text-orange-700',
};

const WITHDRAWABLE_STATUSES = new Set(['Draft', 'Submitted', 'Docs Pending', 'Docs Verified']);

function WithdrawModal({ appNumber, onConfirm, onCancel, loading }) {
  const [reason, setReason] = useState('');
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6 space-y-4">
        <h2 className="text-lg font-bold text-gray-900">Withdraw {appNumber}?</h2>
        <p className="text-sm text-gray-600">
          This cannot be undone. The application will be marked as <strong>Withdrawn</strong>.
        </p>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Reason <span className="text-gray-400 font-normal">(optional)</span>
          </label>
          <textarea value={reason} onChange={e => setReason(e.target.value)} rows={3}
            placeholder="e.g. No longer need the loan…"
            className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-red-400" />
        </div>
        <div className="flex gap-3 justify-end pt-1">
          <button onClick={onCancel} disabled={loading}
            className="px-4 py-2 border border-gray-300 text-gray-700 text-sm font-medium rounded-lg hover:bg-gray-50 disabled:opacity-50">
            Cancel
          </button>
          <button onClick={() => onConfirm(reason)} disabled={loading}
            className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50">
            {loading ? 'Withdrawing…' : 'Yes, Withdraw'}
          </button>
        </div>
      </div>
    </div>
  );
}

export default function ApplicationsPage() {
  const [apps,           setApps]           = useState([]);
  const [loading,        setLoading]        = useState(true);
  const [error,          setError]          = useState(null);
  const [withdrawTarget, setWithdrawTarget] = useState(null);
  const [withdrawing,    setWithdrawing]    = useState(false);

  const load = () => {
    setLoading(true);
    getMyApplications()
      .then(r => setApps(r.data))
      .catch(e => setError(e.response?.data?.message ?? 'Failed to load applications.'))
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, []);

  const handleWithdraw = async (reason) => {
    setWithdrawing(true);
    try {
      await withdrawApplication(withdrawTarget.id, reason);
      setWithdrawTarget(null);
      load();
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to withdraw.');
      setWithdrawTarget(null);
    } finally {
      setWithdrawing(false);
    }
  };

  return (
    <div className="max-w-4xl mx-auto px-4 py-8">
      {withdrawTarget && (
        <WithdrawModal
          appNumber={withdrawTarget.applicationNumber}
          onConfirm={handleWithdraw}
          onCancel={() => setWithdrawTarget(null)}
          loading={withdrawing}
        />
      )}

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">My Loan Applications</h1>
        <Link to="/applications/new"
          className="flex items-center gap-2 bg-[#1e3a5f] hover:bg-[#0f2744] text-white text-sm font-semibold px-4 py-2 rounded-lg transition-colors">
          <PlusCircle size={15} /> New Application
        </Link>
      </div>

      {loading && <p className="text-gray-500 text-sm">Loading…</p>}
      {error   && <p className="text-red-600 text-sm">{error}</p>}

      {!loading && apps.length === 0 && (
        <div className="text-center py-16 text-gray-400">
          <p className="text-lg mb-2">No applications yet</p>
          <Link to="/applications/new" className="text-blue-600 hover:underline text-sm">
            Start your first loan application →
          </Link>
        </div>
      )}

      <div className="space-y-3">
        {apps.map(app => {
          const canWithdraw = WITHDRAWABLE_STATUSES.has(app.status);
          return (
            <div key={app.id}
              className="bg-white border border-gray-200 rounded-xl px-5 py-4 hover:shadow-sm transition-shadow">
              <div className="flex items-center justify-between gap-3">
                <Link to={`/applications/${app.id}/status`} className="flex-1 min-w-0">
                  <p className="font-semibold text-gray-900 text-sm">{app.applicationNumber}</p>
                  <p className="text-xs text-gray-500 mt-0.5">
                    ₹{Number(app.loanDetails?.requestedAmount ?? app.requestedAmount ?? 0).toLocaleString()} ·{' '}
                    {app.loanDetails?.requestedTenureMonths ?? app.requestedTenureMonths} months
                  </p>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {new Date(app.createdAtUtc).toLocaleDateString()}
                  </p>
                </Link>

                <div className="flex items-center gap-2 flex-shrink-0">
                  <span className={`text-xs font-semibold px-2.5 py-1 rounded-full ${STATUS_COLOUR[app.status] ?? 'bg-gray-100 text-gray-600'}`}>
                    {app.status}
                  </span>

                  {canWithdraw && (
                    <button
                      onClick={() => setWithdrawTarget({ id: app.id, applicationNumber: app.applicationNumber })}
                      title="Withdraw application"
                      className="flex items-center gap-1 text-xs bg-red-50 hover:bg-red-100 text-red-700 border border-red-200 px-2.5 py-1.5 rounded-lg font-semibold transition-colors">
                      <LogOut size={12} /> Withdraw
                    </button>
                  )}

                  <Link to={`/applications/${app.id}/status`}>
                    <ChevronRight size={16} className="text-gray-400" />
                  </Link>
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* AI Chat Widget — context from most recent application */}
      <AiChatWidget applicationContext={apps[0] ?? null} />
    </div>
  );
}
