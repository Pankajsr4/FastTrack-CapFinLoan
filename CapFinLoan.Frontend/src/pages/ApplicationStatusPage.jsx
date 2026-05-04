import { useState, useEffect } from 'react';
import { useParams, Link, useNavigate, useLocation } from 'react-router-dom';
import { getApplicationStatus, getApplicationById, withdrawApplication } from '../services/applicationService';
import { getDocumentsByApplication } from '../services/documentService';
import DocumentStatusBadge from '../components/DocumentStatusBadge';
import AiChatWidget from '../components/AiChatWidget';

const APP_STEPS = ['Draft', 'Submitted', 'Docs Pending', 'Docs Verified', 'Under Review', 'Approved'];

const STATUS_ICON = {
  Draft: '📝', Submitted: '📤', 'Docs Pending': '📎',
  'Docs Verified': '✅', 'Under Review': '🔍',
  Approved: '🎉', Rejected: '❌', Closed: '🔒', Withdrawn: '↩️',
};

// Statuses where the applicant may withdraw
const WITHDRAWABLE_STATUSES = new Set(['Draft', 'Submitted', 'Docs Pending', 'Docs Verified']);

// ── Withdraw confirmation modal ───────────────────────────────────────────────
function WithdrawModal({ onConfirm, onCancel, loading }) {
  const [reason, setReason] = useState('');
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6 space-y-4">
        <h2 className="text-lg font-bold text-gray-900">Withdraw Application?</h2>
        <p className="text-sm text-gray-600">
          This action cannot be undone. The application will be marked as <strong>Withdrawn</strong> and can no longer be edited or submitted.
        </p>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Reason <span className="text-gray-400 font-normal">(optional)</span>
          </label>
          <textarea
            value={reason}
            onChange={e => setReason(e.target.value)}
            rows={3}
            placeholder="e.g. No longer need the loan…"
            className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-red-400"
          />
        </div>
        <div className="flex gap-3 justify-end pt-1">
          <button
            onClick={onCancel}
            disabled={loading}
            className="px-4 py-2 border border-gray-300 text-gray-700 text-sm font-medium rounded-lg hover:bg-gray-50 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            onClick={() => onConfirm(reason)}
            disabled={loading}
            className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-semibold rounded-lg disabled:opacity-50"
          >
            {loading ? 'Withdrawing…' : 'Yes, Withdraw'}
          </button>
        </div>
      </div>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────
export default function ApplicationStatusPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation?.() ?? {};

  const [app,      setApp]      = useState(null);
  const [status,   setStatus]   = useState(null);
  const [docs,     setDocs]     = useState([]);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState(null);
  const [success,  setSuccess]  = useState(location.state?.success ?? null);

  const [showWithdrawModal, setShowWithdrawModal] = useState(false);
  const [withdrawing,       setWithdrawing]       = useState(false);

  const load = () => {
    if (!id) return;
    setLoading(true);
    Promise.all([
      getApplicationById(id),
      getApplicationStatus(id),
      getDocumentsByApplication(id).catch(() => ({ data: [] })),
    ])
      .then(([appRes, statusRes, docsRes]) => {
        setApp(appRes.data);
        setStatus(statusRes.data);
        setDocs(docsRes.data ?? []);
      })
      .catch(e => setError(e.response?.data?.message ?? 'Failed to load.'))
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, [id]);

  const handleWithdraw = async (reason) => {
    setWithdrawing(true);
    try {
      const { data } = await withdrawApplication(id, reason);
      setApp(data);
      setShowWithdrawModal(false);
      setSuccess('Application withdrawn successfully.');
      // Reload status timeline
      const statusRes = await getApplicationStatus(id);
      setStatus(statusRes.data);
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to withdraw application.');
      setShowWithdrawModal(false);
    } finally {
      setWithdrawing(false);
    }
  };

  if (loading) return (
    <div className="flex justify-center py-16">
      <div className="w-8 h-8 border-4 border-gray-200 border-t-[#1e3a5f] rounded-full animate-spin" />
    </div>
  );
  if (error && !app) return <p className="text-center text-red-600 py-8">{error}</p>;
  if (!app) return null;

  const currentIdx = APP_STEPS.indexOf(app.status);
  const canWithdraw = WITHDRAWABLE_STATUSES.has(app.status);
  const isWithdrawn = app.status === 'Withdrawn';

  // Build context for AI assistant
  const aiContext = {
    applicationNumber: app.applicationNumber,
    status:            app.status,
    requestedAmount:   app.loanDetails?.requestedAmount ?? app.requestedAmount,
    tenureMonths:      app.loanDetails?.requestedTenureMonths ?? app.requestedTenureMonths,
    loanPurpose:       app.loanDetails?.loanPurpose ?? app.loanPurpose,
    monthlyIncome:     app.employmentDetails?.monthlyIncome ?? app.monthlyIncome,
    existingEmiAmount: app.employmentDetails?.existingEmiAmount ?? app.existingEmiAmount,
    uploadedDocumentTypes: docs.map(d => d.documentType),
  };

  return (
    <div className="max-w-3xl mx-auto px-4 py-8 space-y-6">
      {showWithdrawModal && (
        <WithdrawModal
          onConfirm={handleWithdraw}
          onCancel={() => setShowWithdrawModal(false)}
          loading={withdrawing}
        />
      )}

      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-3">
          <Link to="/applications" className="text-gray-500 hover:text-gray-800 text-sm">← My Applications</Link>
          <h1 className="text-xl font-bold text-gray-900">{app.applicationNumber}</h1>
          {app.isEdited && (
            <span className="text-xs bg-orange-100 text-orange-700 px-2 py-0.5 rounded-full font-semibold">
              Edited {app.editCount > 1 ? `(×${app.editCount})` : ''}
            </span>
          )}
        </div>

        {/* Action buttons */}
        {!isWithdrawn && canWithdraw && (
          <div className="flex gap-2">
            <button
              onClick={() => setShowWithdrawModal(true)}
              className="text-xs bg-red-50 hover:bg-red-100 text-red-700 border border-red-200 px-3 py-1.5 rounded-lg font-semibold transition-colors"
            >
              ↩️ Withdraw
            </button>
          </div>
        )}
      </div>

      {/* Success / error banners */}
      {success && (
        <div className="bg-green-50 border border-green-200 text-green-800 text-sm rounded-lg px-4 py-3">
          {success}
        </div>
      )}
      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3">
          {error}
        </div>
      )}

      {/* Withdrawn notice */}
      {isWithdrawn && (
        <div className="bg-gray-50 border border-gray-200 rounded-xl p-4">
          <p className="text-sm font-semibold text-gray-700">↩️ This application has been withdrawn.</p>
          {app.withdrawalReason && (
            <p className="text-xs text-gray-500 mt-1">Reason: {app.withdrawalReason}</p>
          )}
          {app.withdrawnAtUtc && (
            <p className="text-xs text-gray-400 mt-0.5">
              Withdrawn on {new Date(app.withdrawnAtUtc).toLocaleString()}
            </p>
          )}
        </div>
      )}

      {/* Status pipeline */}
      {!isWithdrawn && (
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6">
          <div className="flex items-start mb-6">
            {APP_STEPS.map((s, i) => {
              const done   = i < currentIdx;
              const active = i === currentIdx;
              const fail   = (app.status === 'Rejected' || app.status === 'Closed') && i === currentIdx;
              return (
                <div key={s} className="flex-1 flex flex-col items-center relative">
                  {i > 0 && <div className={`absolute top-3 right-1/2 w-full h-0.5 ${done ? 'bg-[#1e3a5f]' : 'bg-gray-200'}`} />}
                  <div className={`relative z-10 w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold
                    ${fail   ? 'bg-red-500 text-white ring-4 ring-red-200'
                    : active ? 'bg-[#1e3a5f] text-white ring-4 ring-blue-200'
                    : done   ? 'bg-[#1e3a5f] text-white'
                    :          'bg-gray-200 text-gray-400'}`}>
                    {done ? '✓' : i + 1}
                  </div>
                  <span className={`mt-1 text-[9px] text-center leading-tight font-medium
                    ${active ? 'text-[#1e3a5f]' : done ? 'text-gray-600' : 'text-gray-400'}`}>
                    {s}
                  </span>
                </div>
              );
            })}
          </div>

          {/* Current status card */}
          <div className="flex items-center gap-3 bg-blue-50 rounded-xl p-4">
            <span className="text-2xl">{STATUS_ICON[app.status] ?? '📋'}</span>
            <div className="flex-1">
              <p className="font-semibold text-gray-900">{app.status}</p>
              <p className="text-xs text-gray-500">
                ₹{Number(app.loanDetails?.requestedAmount ?? app.requestedAmount ?? 0).toLocaleString()} ·{' '}
                {app.loanDetails?.requestedTenureMonths ?? app.requestedTenureMonths} months ·{' '}
                {app.loanDetails?.loanPurpose ?? app.loanPurpose}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Status timeline */}
      {status?.timeline?.length > 0 && (
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6">
          <h2 className="font-semibold text-gray-800 mb-4">Status Timeline</h2>
          <ol className="relative border-l border-gray-200 space-y-4 ml-3">
            {[...status.timeline].reverse().map((t, i) => (
              <li key={i} className="ml-4">
                <div className="absolute -left-1.5 w-3 h-3 rounded-full bg-[#1e3a5f] border-2 border-white" />
                <p className="text-sm font-medium text-gray-900">
                  {t.fromStatus ? `${t.fromStatus} → ` : ''}{t.toStatus}
                </p>
                {t.remarks && <p className="text-xs text-gray-500 mt-0.5">{t.remarks}</p>}
                <p className="text-xs text-gray-400 mt-0.5">{new Date(t.changedAtUtc).toLocaleString()}</p>
              </li>
            ))}
          </ol>
        </div>
      )}

      {/* Documents */}
      {docs.length > 0 && (
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-semibold text-gray-800">Documents</h2>
            {!isWithdrawn && (
              <Link to="/documents/upload" className="text-xs text-blue-600 hover:underline">+ Upload more</Link>
            )}
          </div>
          <div className="space-y-2">
            {docs.map(doc => (
              <div key={doc.id} className="flex items-center justify-between py-2 border-b border-gray-100 last:border-0">
                <div>
                  <p className="text-sm font-medium text-gray-800">{doc.documentType}</p>
                  <p className="text-xs text-gray-400">{doc.fileName} · {(doc.fileSizeBytes / 1024).toFixed(1)} KB</p>
                </div>
                <DocumentStatusBadge status={doc.status} />
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Upload CTA */}
      {app.status === 'Docs Pending' && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-xl p-4 flex items-center justify-between">
          <p className="text-sm text-yellow-800 font-medium">Documents required — please upload your KYC and income proof.</p>
          <Link to="/documents/upload" className="text-xs bg-yellow-600 hover:bg-yellow-700 text-white px-3 py-1.5 rounded-lg font-semibold">
            Upload Now
          </Link>
        </div>
      )}

      {/* AI Chat Widget with application context */}
      <AiChatWidget applicationContext={aiContext} />
    </div>
  );
}
