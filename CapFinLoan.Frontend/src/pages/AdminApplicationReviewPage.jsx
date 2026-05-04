import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getAdminApplicationById, reviewApplication, disburseApplication, getAdminDocuments, verifyDocument } from '../services/adminService';
import DocumentStatusBadge from '../components/DocumentStatusBadge';

const ALL_STATUSES = [
  { label: 'Under Review',    value: 'UnderReview'      },
  { label: 'Docs Pending',    value: 'PendingDocuments' },
  { label: 'Approved',        value: 'Approved'         },
  { label: 'Rejected',        value: 'Rejected'         },
  { label: 'Disbursed',       value: 'Disbursed'        },
];

// Valid next statuses per current status (mirrors backend transition rules)
const VALID_TRANSITIONS = {
  'Submitted':     ['PendingDocuments', 'UnderReview', 'Approved', 'Rejected'],
  'Docs Pending':  ['UnderReview', 'Approved', 'Rejected'],
  'Docs Verified': ['PendingDocuments', 'UnderReview', 'Approved', 'Rejected'],
  'Under Review':  ['PendingDocuments', 'Approved', 'Rejected'],
  'Approved':      ['Disbursed'],
  'Rejected':      [],
  'Disbursed':     [],
  'Withdrawn':     [],
  'Draft':         [],
};

function getAvailableStatuses(currentStatus) {
  const allowed = VALID_TRANSITIONS[currentStatus] ?? [];
  return ALL_STATUSES.filter(s => allowed.includes(s.value));
}

export default function AdminApplicationReviewPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [app,      setApp]      = useState(null);
  const [docs,     setDocs]     = useState([]);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState(null);
  const [saving,   setSaving]   = useState(false);

  const [decision, setDecision] = useState({ targetStatus: 'UnderReview', remarks: '' });
  const [disbursedAmount, setDisbursedAmount] = useState('');
  const [verifying, setVerifying] = useState(null);

  useEffect(() => {
    Promise.all([
      getAdminApplicationById(id),
      getAdminDocuments(id).catch(() => ({ data: [] })),
    ])
      .then(([appRes, docsRes]) => {
        setApp(appRes.data);
        setDocs(docsRes.data ?? []);
        // Set default to first valid transition for this status
        const available = getAvailableStatuses(appRes.data.status);
        const defaultStatus = available.length > 0 ? available[0].value : '';
        setDecision(d => ({ ...d, targetStatus: defaultStatus }));
      })
      .catch(e => setError(e.response?.data?.message ?? 'Failed to load.'))
      .finally(() => setLoading(false));
  }, [id]);

  const handleReview = async () => {
    setSaving(true); setError(null);
    try {
      // Disbursement uses a separate endpoint that requires a disbursed amount
      if (decision.targetStatus === 'Disbursed') {
        const amount = parseFloat(disbursedAmount);
        if (!amount || amount <= 0) {
          setError('Please enter a valid disbursed amount greater than zero.');
          setSaving(false);
          return;
        }
        await disburseApplication(id, { disbursedAmount: amount });
      } else {
        await reviewApplication(id, decision);
      }
      navigate('/admin/applications');
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to save decision.');
    } finally {
      setSaving(false);
    }
  };

  const handleVerifyDoc = async (docId, isVerified, remarks) => {
    setVerifying(docId);
    try {
      await verifyDocument(docId, { isVerified, remarks });
      setDocs(prev => prev.map(d => d.id === docId
        ? { ...d, status: isVerified ? 'Verified' : 'ReuploadRequired' }
        : d));
    } catch (e) {
      setError(e.response?.data?.message ?? 'Failed to verify document.');
    } finally {
      setVerifying(null);
    }
  };

  if (loading) return <div className="flex justify-center py-16"><div className="w-8 h-8 border-4 border-gray-200 border-t-[#1e3a5f] rounded-full animate-spin" /></div>;
  if (!app)    return <p className="text-center text-red-600 py-8">{error}</p>;

  const p = app.personalDetails ?? app;   // flat response fallback
  const e = app.employmentDetails ?? app;
  const l = app.loanDetails ?? app;

  const availableStatuses = getAvailableStatuses(app.status);

  // Flat field helpers (admin API returns flat structure)
  const name    = app.personalDetails
    ? `${p.firstName ?? ''} ${p.lastName ?? ''}`.trim()
    : `${app.firstName ?? ''} ${app.lastName ?? ''}`.trim() || (app.applicantName ?? '—');
  const email   = p.email   ?? app.email   ?? '—';
  const phone   = p.phone   ?? app.phone   ?? '—';
  const city    = p.city    ?? app.city    ?? '';
  const state   = p.state   ?? app.state   ?? '';
  const addr1   = p.addressLine1 ?? app.addressLine1 ?? '';
  const dob     = p.dateOfBirth  ?? app.dateOfBirth;
  const amount  = l.requestedAmount       ?? app.requestedAmount       ?? 0;
  const tenure  = l.requestedTenureMonths ?? app.requestedTenureMonths ?? 0;
  const purpose = l.loanPurpose  ?? app.loanPurpose  ?? '—';
  const employer= e.employerName ?? app.employerName ?? '—';
  const income  = e.monthlyIncome    ?? app.monthlyIncome    ?? 0;
  const emi     = e.existingEmiAmount ?? app.existingEmiAmount ?? 0;

  return (
    <div className="max-w-4xl mx-auto px-4 py-8 space-y-6">
      <div className="flex items-center gap-3 flex-wrap">
        <button onClick={() => navigate('/admin/applications')} className="text-gray-500 hover:text-gray-800 text-sm">← Back</button>
        <h1 className="text-xl font-bold text-gray-900">{app.applicationNumber}</h1>
        <span className="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full font-semibold">{app.status}</span>
        {app.isEdited && (
          <span className="text-xs bg-orange-100 text-orange-700 px-2 py-0.5 rounded-full font-semibold">
            ✏️ Edited ×{app.editCount}
          </span>
        )}
        {app.isEdited && app.lastModifiedAt && (
          <span className="text-xs text-gray-400">
            Last modified: {new Date(app.lastModifiedAt).toLocaleString()}
          </span>
        )}
      </div>

      {error && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-3">{error}</div>}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Applicant info */}
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5 space-y-3">
          <h2 className="font-semibold text-gray-800 border-b pb-2">Applicant</h2>
          <Info label="Name"    value={name} />
          <Info label="Email"   value={email} />
          <Info label="Phone"   value={phone} />
          <Info label="Address" value={[addr1, city, state].filter(Boolean).join(', ') || '—'} />
          <Info label="DOB"     value={dob ? new Date(dob).toLocaleDateString() : '—'} />
        </div>

        {/* Loan details */}
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5 space-y-3">
          <h2 className="font-semibold text-gray-800 border-b pb-2">Loan Details</h2>
          <Info label="Amount"   value={`₹${Number(amount).toLocaleString()}`} />
          <Info label="Tenure"   value={`${tenure} months`} />
          <Info label="Purpose"  value={purpose} />
          <Info label="Employer" value={employer} />
          <Info label="Income"   value={`₹${Number(income).toLocaleString()}/mo`} />
          <Info label="EMI"      value={`₹${Number(emi).toLocaleString()}/mo`} />
        </div>
      </div>

      {/* Documents */}
      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5">
        <h2 className="font-semibold text-gray-800 mb-4">Documents</h2>
        {docs.length === 0 && <p className="text-sm text-gray-400">No documents uploaded yet.</p>}
        <div className="space-y-3">
          {docs.map(doc => (
            <div key={doc.id} className="flex items-center justify-between border border-gray-100 rounded-xl px-4 py-3">
              <div>
                <p className="text-sm font-medium text-gray-800">{doc.documentType}</p>
                <p className="text-xs text-gray-400">{doc.fileName} · {(doc.fileSizeBytes / 1024).toFixed(1)} KB</p>
              </div>
              <div className="flex items-center gap-3">
                <DocumentStatusBadge status={doc.status} />
                <a
                  href={`http://localhost:5000/gateway/admin/documents/${doc.id}/download`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-xs bg-gray-100 hover:bg-gray-200 text-gray-700 px-2.5 py-1 rounded-lg"
                  onClick={(e) => {
                    e.preventDefault();
                    const token = localStorage.getItem('token');
                    fetch(`http://localhost:5000/gateway/admin/documents/${doc.id}/download`, {
                      headers: { Authorization: `Bearer ${token}` }
                    }).then(r => r.blob()).then(blob => {
                      const url = URL.createObjectURL(blob);
                      window.open(url, '_blank');
                    });
                  }}
                >
                  👁 View
                </a>
                {(doc.status === 'UnderReview' || doc.status === 'Pending') && (
                  <div className="flex gap-2">
                    <button onClick={() => handleVerifyDoc(doc.id, true, 'Document verified.')}
                      disabled={verifying === doc.id}
                      className="text-xs bg-green-600 hover:bg-green-700 text-white px-2.5 py-1 rounded-lg disabled:opacity-50">
                      ✓ Approve
                    </button>
                    <button onClick={() => handleVerifyDoc(doc.id, false, 'Document rejected. Please re-upload.')}
                      disabled={verifying === doc.id}
                      className="text-xs bg-red-600 hover:bg-red-700 text-white px-2.5 py-1 rounded-lg disabled:opacity-50">
                      ✗ Reject
                    </button>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Decision panel */}
      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5 space-y-4">
        <h2 className="font-semibold text-gray-800">Admin Decision</h2>

        {availableStatuses.length === 0 ? (
          <div className="bg-gray-50 border border-gray-200 rounded-xl px-4 py-3 text-sm text-gray-500">
            {app.status === 'Disbursed'  && '✅ This loan has been disbursed. No further status changes are possible.'}
            {app.status === 'Rejected'   && '❌ This application has been rejected. No further status changes are possible.'}
            {app.status === 'Withdrawn'  && '↩️ This application was withdrawn by the applicant.'}
            {!['Disbursed','Rejected','Withdrawn'].includes(app.status) && 'No status transitions available from the current state.'}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Update Status</label>
                <select value={decision.targetStatus} onChange={e => setDecision(d => ({ ...d, targetStatus: e.target.value }))}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white">
                  {availableStatuses.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
                </select>
              </div>
            </div>

            {/* Disbursed amount — only shown when Disbursed is selected */}
            {decision.targetStatus === 'Disbursed' && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Disbursed Amount <span className="text-red-500">*</span>
                </label>
                <div className="relative max-w-xs">
                  <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500 text-sm">₹</span>
                  <input
                    type="number"
                    min="1"
                    step="1"
                    value={disbursedAmount}
                    onChange={e => setDisbursedAmount(e.target.value)}
                    placeholder={`e.g. ${Number(amount).toLocaleString()}`}
                    className="w-full pl-7 pr-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <p className="text-xs text-gray-400 mt-1">
                  Requested amount: ₹{Number(amount).toLocaleString()}
                </p>
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Remarks
                {(decision.targetStatus === 'Rejected' || decision.targetStatus === 'PendingDocuments') && (
                  <span className="text-red-500 ml-1">*required</span>
                )}
              </label>
              <textarea value={decision.remarks} onChange={e => setDecision(d => ({ ...d, remarks: e.target.value }))} rows={3}
                placeholder="Add notes for the applicant…"
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <button onClick={handleReview} disabled={saving}
              className="px-5 py-2 bg-[#1e3a5f] hover:bg-[#0f2744] text-white text-sm font-semibold rounded-lg disabled:opacity-60">
              {saving ? 'Saving…' : 'Save Decision'}
            </button>
          </>
        )}
      </div>
    </div>
  );
}

function Info({ label, value }) {
  return (
    <div className="flex gap-3 text-sm">
      <span className="text-gray-400 w-20 shrink-0">{label}</span>
      <span className="text-gray-800">{value || '—'}</span>
    </div>
  );
}
