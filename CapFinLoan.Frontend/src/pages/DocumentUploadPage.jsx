import { useState, useRef, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useUploadDocument, UPLOAD_PHASE } from '../hooks/useUploadDocument';
import { getMyApplications } from '../services/applicationService';

const DOCUMENT_TYPES = ['NationalId', 'ProofOfIncome', 'BankStatement', 'AddressProof', 'Other'];
const ALLOWED_MIME   = ['application/pdf', 'image/jpeg', 'image/png'];
const MAX_BYTES      = 5 * 1024 * 1024;

function fileIcon(type) {
  if (type === 'application/pdf') return '📄';
  if (type?.startsWith('image/'))  return '🖼️';
  return '📎';
}

// ── Upload phase progress bar ─────────────────────────────────────────────────
const PHASES = [
  { key: UPLOAD_PHASE.Preparing,  label: 'Preparing'  },
  { key: UPLOAD_PHASE.Uploading,  label: 'Uploading'  },
  { key: UPLOAD_PHASE.Processing, label: 'Processing' },
  { key: UPLOAD_PHASE.Done,       label: 'Done'       },
];

function UploadPhaseBar({ phase, progress }) {
  const activeIdx = PHASES.findIndex((p) => p.key === phase);

  return (
    <div className="space-y-3">
      {/* Step indicators */}
      <div className="flex items-center">
        {PHASES.map((p, i) => {
          const done   = i < activeIdx || phase === UPLOAD_PHASE.Done;
          const active = i === activeIdx && phase !== UPLOAD_PHASE.Done;
          return (
            <div key={p.key} className="flex-1 flex flex-col items-center relative">
              {i > 0 && (
                <div className={`absolute top-3 right-1/2 w-full h-0.5 transition-colors duration-500 ${done ? 'bg-blue-500' : 'bg-gray-200'}`} />
              )}
              <div className={`relative z-10 w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold transition-all duration-300
                ${done   ? 'bg-blue-500 text-white'
                : active ? 'bg-[#1e3a5f] text-white ring-4 ring-blue-200'
                :          'bg-gray-200 text-gray-400'}`}
              >
                {done ? '✓' : active ? (
                  <span className="w-2.5 h-2.5 border-2 border-white border-t-transparent rounded-full animate-spin block" />
                ) : i + 1}
              </div>
              <span className={`mt-1 text-[10px] font-medium ${active ? 'text-[#1e3a5f]' : done ? 'text-gray-600' : 'text-gray-400'}`}>
                {p.label}
              </span>
            </div>
          );
        })}
      </div>

      {/* Byte progress bar — only visible during Uploading */}
      {phase === UPLOAD_PHASE.Uploading && (
        <div>
          <div className="flex justify-between text-xs text-gray-500 mb-1">
            <span>Uploading file…</span>
            <span>{progress}%</span>
          </div>
          <div className="w-full bg-gray-200 rounded-full h-1.5 overflow-hidden">
            <div
              className="h-1.5 bg-gradient-to-r from-[#1e3a5f] to-blue-400 rounded-full transition-all duration-200"
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>
      )}

      {phase === UPLOAD_PHASE.Processing && (
        <p className="text-xs text-blue-600 text-center animate-pulse">Server is processing your file…</p>
      )}
    </div>
  );
}

// ── Error card with retry ─────────────────────────────────────────────────────
function UploadErrorCard({ message, onRetry, onReset }) {
  return (
    <div className="bg-red-50 border border-red-200 rounded-xl p-5 text-center">
      <div className="text-3xl mb-3">❌</div>
      <h3 className="font-semibold text-red-800 mb-1">Upload failed</h3>
      <p className="text-sm text-red-700 mb-4">{message}</p>
      <div className="flex gap-3 justify-center">
        <button onClick={onReset} className="px-4 py-2 border border-red-300 text-red-700 text-sm font-medium rounded-lg hover:bg-red-100 transition-colors">
          Start over
        </button>
        <button onClick={onRetry} className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white text-sm font-semibold rounded-lg transition-colors">
          ↻ Retry upload
        </button>
      </div>
    </div>
  );
}

// ── Success card ──────────────────────────────────────────────────────────────
function SuccessCard({ documentId, fileName, onUploadAnother }) {
  const [copied, setCopied] = useState(false);
  const copy = () => { navigator.clipboard.writeText(documentId); setCopied(true); setTimeout(() => setCopied(false), 2000); };

  return (
    <div className="bg-white rounded-2xl shadow-lg p-8 max-w-md mx-auto text-center">
      <div className="text-5xl mb-4">✅</div>
      <h2 className="text-xl font-bold text-green-800 mb-2">Upload successful</h2>
      <p className="text-sm text-gray-600 mb-6"><strong>{fileName}</strong> has been uploaded and is being processed.</p>

      <div className="bg-gray-50 border border-gray-200 rounded-xl p-4 mb-6 text-left">
        <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Document ID</p>
        <div className="flex items-center gap-2">
          <code className="flex-1 text-xs bg-white border border-gray-200 rounded-lg px-3 py-2 font-mono text-gray-800 break-all">{documentId}</code>
          <button onClick={copy} className="shrink-0 px-3 py-2 bg-[#1e3a5f] text-white text-xs font-semibold rounded-lg hover:bg-[#0f2744] transition-colors">
            {copied ? '✓' : 'Copy'}
          </button>
        </div>
        <p className="text-xs text-gray-400 mt-2">Save this ID to track processing status.</p>
      </div>

      <div className="flex gap-3 justify-center">
        <button onClick={onUploadAnother} className="px-4 py-2 border border-gray-300 text-gray-700 text-sm font-medium rounded-lg hover:bg-gray-50 transition-colors">
          Upload another
        </button>
        <Link to={`/documents/${documentId}/status`} className="px-4 py-2 bg-[#1e3a5f] text-white text-sm font-semibold rounded-lg hover:bg-[#0f2744] transition-colors">
          Track status →
        </Link>
      </div>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────
export default function DocumentUploadPage() {
  const navigate     = useNavigate();
  const fileInputRef = useRef(null);

  const [applicationId, setApplicationId] = useState('');
  const [documentType,  setDocumentType]  = useState(DOCUMENT_TYPES[0]);
  const [file,          setFile]          = useState(null);
  const [fileError,     setFileError]     = useState(null);
  const [dragOver,      setDragOver]      = useState(false);
  const [applications,  setApplications]  = useState([]);

  // Load user's submitted applications for the dropdown
  useEffect(() => {
    getMyApplications().then(({ data }) => {
      const submitted = (data ?? []).filter(a => a.status !== 'Draft');
      setApplications(submitted);
      if (submitted.length === 1) setApplicationId(submitted[0].id);
    }).catch(() => {});
  }, []);

  const { upload, retry, reset, phase, uploading, progress, documentId, error } = useUploadDocument();

  const validateAndSetFile = (f) => {
    if (!f) return;
    if (!ALLOWED_MIME.includes(f.type)) { setFileError('Only PDF, JPG, and PNG files are allowed.'); setFile(null); return; }
    if (f.size > MAX_BYTES)             { setFileError('File must be 5 MB or smaller.'); setFile(null); return; }
    setFileError(null); setFile(f);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!file) { setFileError('Please select a file.'); return; }
    await upload(applicationId, documentType, file);
  };

  const handleReset = () => {
    reset(); setFile(null); setFileError(null); setApplicationId(''); setDocumentType(DOCUMENT_TYPES[0]);
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  // ── Success ───────────────────────────────────────────────────────────────
  if (phase === UPLOAD_PHASE.Done && documentId) {
    return (
      <div className="max-w-5xl mx-auto px-4 py-8">
        <SuccessCard documentId={documentId} fileName={file?.name ?? 'Document'} onUploadAnother={handleReset} />
      </div>
    );
  }

  return (
    <div className="max-w-xl mx-auto px-4 py-8">
      <div className="flex items-center gap-3 mb-6">
        <button onClick={() => navigate('/documents')} className="text-gray-500 hover:text-gray-800 text-sm transition-colors">← Back</button>
        <h1 className="text-2xl font-bold text-gray-900">Upload Document</h1>
      </div>

      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6 space-y-5">

        {/* Upload phase bar — shown while in-flight */}
        {uploading && (
          <div className="pb-2">
            <UploadPhaseBar phase={phase} progress={progress} />
          </div>
        )}

        {/* Error with retry */}
        {phase === UPLOAD_PHASE.Error && (
          <UploadErrorCard message={error} onRetry={retry} onReset={handleReset} />
        )}

        {/* Form — hidden while uploading or errored */}
        {phase !== UPLOAD_PHASE.Error && (
          <form onSubmit={handleSubmit} className="space-y-5" noValidate>
            {/* Application ID */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5" htmlFor="appId">Application ID</label>
              {applications.length > 0 ? (
                <select
                  id="appId"
                  value={applicationId}
                  onChange={(e) => setApplicationId(e.target.value)}
                  required
                  disabled={uploading}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white disabled:bg-gray-50"
                >
                  <option value="">— Select application —</option>
                  {applications.map(a => (
                    <option key={a.id} value={a.id}>
                      {a.applicationNumber} · ₹{Number(a.loanDetails?.requestedAmount ?? 0).toLocaleString()} · {a.status}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  id="appId"
                  value={applicationId}
                  onChange={(e) => setApplicationId(e.target.value)}
                  required
                  disabled={uploading}
                  placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 font-mono disabled:bg-gray-50 disabled:text-gray-400"
                />
              )}
            </div>

            {/* Document type */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5" htmlFor="docType">Document Type</label>
              <select
                id="docType"
                value={documentType}
                onChange={(e) => setDocumentType(e.target.value)}
                disabled={uploading}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white disabled:bg-gray-50"
              >
                {DOCUMENT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>

            {/* Drop zone */}
            <div>
              <label className="block text-sm font-semibold text-gray-700 mb-1.5">
                File <span className="font-normal text-gray-400 text-xs">PDF, JPG or PNG — max 5 MB</span>
              </label>
              <div
                onClick={() => !uploading && fileInputRef.current?.click()}
                onDrop={(e) => { e.preventDefault(); setDragOver(false); validateAndSetFile(e.dataTransfer.files[0]); }}
                onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
                onDragLeave={() => setDragOver(false)}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => e.key === 'Enter' && fileInputRef.current?.click()}
                className={`border-2 border-dashed rounded-xl p-6 transition-colors select-none
                  ${uploading ? 'cursor-not-allowed opacity-60'
                  : dragOver  ? 'border-blue-500 bg-blue-50 cursor-copy'
                  : file      ? 'border-green-400 bg-green-50 cursor-pointer'
                  :             'border-gray-300 bg-gray-50 hover:border-gray-400 cursor-pointer'}`}
              >
                {file ? (
                  <div className="flex items-center gap-3">
                    <span className="text-3xl">{fileIcon(file.type)}</span>
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold text-sm text-gray-900 truncate">{file.name}</p>
                      <p className="text-xs text-gray-500 mt-0.5">{(file.size / 1024).toFixed(1)} KB · {file.type}</p>
                    </div>
                    {!uploading && (
                      <button
                        type="button"
                        onClick={(e) => { e.stopPropagation(); setFile(null); if (fileInputRef.current) fileInputRef.current.value = ''; }}
                        className="text-red-400 hover:text-red-600 text-lg leading-none p-1"
                        aria-label="Remove file"
                      >✕</button>
                    )}
                  </div>
                ) : (
                  <div className="text-center">
                    <div className="text-3xl mb-2">📂</div>
                    <p className="text-sm text-gray-600">Drag & drop or <span className="text-blue-600 font-semibold underline">browse</span></p>
                    <p className="text-xs text-gray-400 mt-1">PDF, JPG, PNG up to 5 MB</p>
                  </div>
                )}
              </div>
              <input ref={fileInputRef} type="file" accept=".pdf,.jpg,.jpeg,.png" onChange={(e) => validateAndSetFile(e.target.files[0])} className="hidden" aria-hidden />
              {fileError && <p className="text-xs text-red-600 mt-1.5">⚠️ {fileError}</p>}
            </div>

            {/* Actions */}
            <div className="flex gap-3 justify-end pt-1">
              <button type="button" onClick={() => navigate('/documents')} disabled={uploading} className="px-4 py-2 border border-gray-300 text-gray-700 text-sm font-medium rounded-lg hover:bg-gray-50 transition-colors disabled:opacity-50">
                Cancel
              </button>
              <button type="submit" disabled={uploading || !file || !applicationId} className="px-5 py-2 bg-[#1e3a5f] hover:bg-[#0f2744] text-white text-sm font-semibold rounded-lg transition-colors disabled:opacity-50">
                {uploading ? 'Uploading…' : 'Upload Document'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
