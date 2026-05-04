import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuthContext } from '../context/AuthContext';
import { getMyApplications } from '../services/applicationService';
import { getDocumentsByUser } from '../services/documentService';
import { PlusCircle, Upload, ChevronRight } from 'lucide-react';
import AiChatWidget from '../components/AiChatWidget';

const STATUS_COLOUR = {
  Draft:          'bg-gray-100 text-gray-600',
  Submitted:      'bg-blue-100 text-blue-700',
  'Docs Pending': 'bg-yellow-100 text-yellow-700',
  'Docs Verified':'bg-teal-100 text-teal-700',
  'Under Review': 'bg-purple-100 text-purple-700',
  Approved:       'bg-green-100 text-green-700',
  Rejected:       'bg-red-100 text-red-700',
};

const DOC_COLOUR = {
  Pending:          'bg-yellow-100 text-yellow-700',
  Processing:       'bg-blue-100 text-blue-700',
  Completed:        'bg-teal-100 text-teal-700',
  UnderReview:      'bg-purple-100 text-purple-700',
  Verified:         'bg-green-100 text-green-700',
  Failed:           'bg-red-100 text-red-700',
  ReuploadRequired: 'bg-orange-100 text-orange-700',
};

export default function DashboardPage() {
  const { user } = useAuthContext();
  const [apps,  setApps]  = useState([]);
  const [docs,  setDocs]  = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      getMyApplications().catch(() => ({ data: [] })),
      user?.sub ? getDocumentsByUser(user.sub).catch(() => ({ data: [] })) : Promise.resolve({ data: [] }),
    ]).then(([appRes, docRes]) => {
      setApps(appRes.data ?? []);
      setDocs(docRes.data ?? []);
    }).finally(() => setLoading(false));
  }, [user?.sub]);

  const docCounts = docs.reduce((a, d) => { a[d.status] = (a[d.status] ?? 0) + 1; return a; }, {});

  return (
    <div className="max-w-5xl mx-auto px-4 py-8 space-y-8">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Welcome back{user?.name ? `, ${user.name.split(' ')[0]}` : ''}</h1>
        <p className="text-gray-500 text-sm mt-1">Here's an overview of your loan applications and documents.</p>
      </div>

      {/* Quick actions */}
      <div className="grid grid-cols-2 gap-4">
        <Link to="/applications/new"
          className="flex items-center gap-3 bg-[#1e3a5f] hover:bg-[#0f2744] text-white rounded-xl p-5 transition-colors">
          <PlusCircle size={22} />
          <div>
            <p className="font-semibold">New Application</p>
            <p className="text-xs text-blue-200">Start a loan application</p>
          </div>
        </Link>
        <Link to="/documents/upload"
          className="flex items-center gap-3 bg-white border border-gray-200 hover:shadow-sm text-gray-800 rounded-xl p-5 transition-shadow">
          <Upload size={22} className="text-[#1e3a5f]" />
          <div>
            <p className="font-semibold">Upload Document</p>
            <p className="text-xs text-gray-400">KYC, income proof, etc.</p>
          </div>
        </Link>
      </div>

      {/* Stats */}
      {!loading && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          {[
            { label: 'Applications', value: apps.length },
            { label: 'Documents',    value: docs.length },
            { label: 'Verified',     value: docCounts['Verified'] ?? 0 },
            { label: 'Pending',      value: (docCounts['Pending'] ?? 0) + (docCounts['Processing'] ?? 0) },
          ].map(({ label, value }) => (
            <div key={label} className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 text-center">
              <p className="text-2xl font-bold text-gray-900">{value}</p>
              <p className="text-xs text-gray-500 mt-0.5">{label}</p>
            </div>
          ))}
        </div>
      )}

      {/* Recent applications */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-semibold text-gray-800">Recent Applications</h2>
          <Link to="/applications" className="text-xs text-blue-600 hover:underline">View all →</Link>
        </div>
        {loading ? <p className="text-sm text-gray-400">Loading…</p> : apps.length === 0 ? (
          <p className="text-sm text-gray-400">No applications yet. <Link to="/applications/new" className="text-blue-600 hover:underline">Start one →</Link></p>
        ) : (
          <div className="space-y-2">
            {apps.slice(0, 3).map(app => (
              <Link key={app.id} to={`/applications/${app.id}/status`}
                className="flex items-center justify-between bg-white border border-gray-200 rounded-xl px-4 py-3 hover:shadow-sm transition-shadow">
                <div>
                  <p className="text-sm font-medium text-gray-900">{app.applicationNumber}</p>
                  <p className="text-xs text-gray-400">₹{Number(app.loanDetails?.requestedAmount ?? 0).toLocaleString()}</p>
                </div>
                <div className="flex items-center gap-2">
                  <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${STATUS_COLOUR[app.status] ?? 'bg-gray-100 text-gray-600'}`}>
                    {app.status}
                  </span>
                  <ChevronRight size={14} className="text-gray-400" />
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>

      {/* Recent documents */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-semibold text-gray-800">Recent Documents</h2>
          <Link to="/documents" className="text-xs text-blue-600 hover:underline">View all →</Link>
        </div>
        {loading ? <p className="text-sm text-gray-400">Loading…</p> : docs.length === 0 ? (
          <p className="text-sm text-gray-400">No documents uploaded yet.</p>
        ) : (
          <div className="space-y-2">
            {docs.slice(0, 4).map(doc => (
              <Link key={doc.id} to={`/documents/${doc.id}/status`}
                className="flex items-center justify-between bg-white border border-gray-200 rounded-xl px-4 py-3 hover:shadow-sm transition-shadow">
                <div>
                  <p className="text-sm font-medium text-gray-800">{doc.documentType}</p>
                  <p className="text-xs text-gray-400">{doc.fileName}</p>
                </div>
                <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${DOC_COLOUR[doc.status] ?? 'bg-gray-100 text-gray-600'}`}>
                  {doc.status}
                </span>
              </Link>
            ))}
          </div>
        )}
      </div>

      {/* AI Chat Widget — passes context from most recent active application */}
      <AiChatWidget applicationContext={apps[0] ?? null} />
    </div>
  );
}
