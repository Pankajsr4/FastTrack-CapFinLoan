import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Upload, RefreshCw, ArrowRight, FolderOpen, AlertCircle } from 'lucide-react';
import { documentApi } from '../services/axiosInstances';
import DocumentStatusBadge from '../components/DocumentStatusBadge';
import { TableRowSkeleton } from '../components/Skeleton';

export default function DocumentListPage() {
  const [documents, setDocuments] = useState([]);
  const [loading,   setLoading]   = useState(true);
  const [error,     setError]     = useState(null);
  const [search,    setSearch]    = useState('');
  const [statusFilter, setStatusFilter] = useState('All');

  const load = () => {
    setLoading(true);
    setError(null);
    documentApi.get('/gateway/documents/my')
      .then(({ data }) => setDocuments(data ?? []))
      .catch(() => setError('Failed to load documents.'))
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, []);

  const filtered = documents
    .filter(d => statusFilter === 'All' || d.status === statusFilter)
    .filter(d => {
      if (!search.trim()) return true;
      const q = search.toLowerCase();
      return d.fileName.toLowerCase().includes(q) || d.documentType.toLowerCase().includes(q);
    });

  const statuses = ['All', ...new Set(documents.map(d => d.status))];

  return (
    <div className="max-w-5xl mx-auto px-4 py-8 fade-in">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">My Documents</h1>
          <p className="text-sm text-gray-500 mt-0.5">All documents you have uploaded</p>
        </div>
        <div className="flex gap-2">
          <button onClick={load} className="inline-flex items-center gap-1.5 px-3 py-2 bg-gray-100 text-gray-700 text-sm font-medium rounded-lg hover:bg-gray-200 transition-colors">
            <RefreshCw size={13} /> Refresh
          </button>
          <Link to="/documents/upload" className="inline-flex items-center gap-1.5 bg-[#1e3a5f] hover:bg-[#0f2744] text-white text-sm font-semibold px-4 py-2 rounded-lg transition-colors">
            <Upload size={14} /> Upload
          </Link>
        </div>
      </div>

      {/* Search and filter */}
      <div className="flex gap-3 mb-4 flex-wrap">
        <div className="relative flex-1 min-w-[200px]">
          <svg className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-4.35-4.35M17 11A6 6 0 1 1 5 11a6 6 0 0 1 12 0z" />
          </svg>
          <input
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Search by file name or type…"
            className="w-full pl-9 pr-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <select
          value={statusFilter}
          onChange={e => setStatusFilter(e.target.value)}
          className="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
        >
          {statuses.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      {error && (
        <div className="flex items-center gap-2 bg-red-50 text-red-800 border border-red-200 rounded-xl px-4 py-3 text-sm mb-4">
          <AlertCircle size={16} className="shrink-0" />{error}
        </div>
      )}

      {!loading && filtered.length === 0 && !error && (
        <div className="text-center py-16 text-gray-400">
          <FolderOpen size={40} className="mx-auto mb-3 opacity-40" />
          <p className="font-medium text-sm">No documents uploaded yet</p>
          <Link to="/documents/upload" className="text-xs text-blue-600 hover:underline mt-1 inline-block">Upload your first document →</Link>
        </div>
      )}

      {(loading || filtered.length > 0) && (
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                {['File Name', 'Type', 'Status', 'Size', 'Uploaded', ''].map((h) => (
                  <th key={h} className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {loading
                ? [1,2,3,4].map((i) => <TableRowSkeleton key={i} cols={6} />)
                : filtered.map((doc) => (
                    <tr key={doc.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-3 font-medium text-gray-900 max-w-[200px] truncate">{doc.fileName}</td>
                      <td className="px-4 py-3 text-gray-500 text-xs">{doc.documentType}</td>
                      <td className="px-4 py-3"><DocumentStatusBadge status={doc.status} /></td>
                      <td className="px-4 py-3 text-gray-500 text-xs">{(doc.fileSizeBytes / 1024).toFixed(1)} KB</td>
                      <td className="px-4 py-3 text-gray-400 text-xs">{new Date(doc.createdAtUtc).toLocaleDateString()}</td>
                      <td className="px-4 py-3 text-right">
                        <Link to={`/documents/${doc.id}/status`} className="inline-flex items-center gap-1 text-blue-600 hover:text-blue-800 text-xs font-medium">
                          Track <ArrowRight size={12} />
                        </Link>
                      </td>
                    </tr>
                  ))
              }
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
