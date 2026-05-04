import { useDocumentStatus } from '../hooks/useDocumentStatus';
import { CONNECTION_STATE } from '../hooks/useSignalR';
import DocumentStatusBadge from './DocumentStatusBadge';

// ── Status metadata ───────────────────────────────────────────────────────────
const STATUS_META = {
  Pending:          { step: 0, label: 'Pending',            desc: 'Waiting to be picked up for processing.',       icon: '⏳', ring: 'ring-yellow-400', bg: 'bg-yellow-50' },
  Processing:       { step: 1, label: 'Processing',         desc: 'Document is being validated and processed.',    icon: '⚙️', ring: 'ring-blue-400',   bg: 'bg-blue-50'   },
  Completed:        { step: 2, label: 'Completed',          desc: 'Processing complete. Queued for admin review.', icon: '✅', ring: 'ring-green-400',  bg: 'bg-green-50'  },
  UnderReview:      { step: 3, label: 'Under Review',       desc: 'An admin is reviewing your document.',          icon: '🔍', ring: 'ring-purple-400', bg: 'bg-purple-50' },
  Verified:         { step: 4, label: 'Verified',           desc: 'Document has been approved.',                   icon: '✔️', ring: 'ring-green-400',  bg: 'bg-green-50'  },
  ReuploadRequired: { step: 4, label: 'Re-upload Required', desc: 'Document was rejected. Please upload again.',   icon: '🔄', ring: 'ring-red-400',    bg: 'bg-red-50'    },
  Failed:           { step: 4, label: 'Failed',             desc: 'Processing failed. Please contact support.',    icon: '❌', ring: 'ring-red-400',    bg: 'bg-red-50'    },
};

const STEPS = ['Pending', 'Processing', 'Completed', 'Under Review', 'Done'];

// ── Connection pill ───────────────────────────────────────────────────────────
const CONN_PILL = {
  [CONNECTION_STATE.Connected]:    'bg-green-100  text-green-700  ring-green-300',
  [CONNECTION_STATE.Connecting]:   'bg-yellow-100 text-yellow-700 ring-yellow-300',
  [CONNECTION_STATE.Reconnecting]: 'bg-yellow-100 text-yellow-700 ring-yellow-300',
  [CONNECTION_STATE.Disconnected]: 'bg-gray-100   text-gray-600   ring-gray-300',
};
const CONN_LABEL = {
  [CONNECTION_STATE.Connected]:    '● Live',
  [CONNECTION_STATE.Connecting]:   '◌ Connecting…',
  [CONNECTION_STATE.Reconnecting]: '↻ Reconnecting…',
  [CONNECTION_STATE.Disconnected]: '○ Polling',
};

// ── Pipeline steps ────────────────────────────────────────────────────────────
function PipelineSteps({ currentStep, isTerminal, status }) {
  const isFailed = status === 'Failed' || status === 'ReuploadRequired';
  return (
    <div className="flex items-start mb-6" role="list">
      {STEPS.map((label, i) => {
        const done   = i < currentStep;
        const active = i === currentStep;
        const fail   = active && isFailed;

        return (
          <div key={label} className="flex-1 flex flex-col items-center relative" role="listitem">
            {/* Connector */}
            {i > 0 && (
              <div className={`absolute top-3 right-1/2 w-full h-0.5 ${done ? 'bg-[#1e3a5f]' : 'bg-gray-200'}`} />
            )}
            {/* Dot */}
            <div className={`relative z-10 w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold transition-all
              ${fail   ? 'bg-red-500 text-white ring-4 ring-red-200'
              : active ? 'bg-[#1e3a5f] text-white ring-4 ring-blue-200'
              : done   ? 'bg-[#1e3a5f] text-white'
              :          'bg-gray-200 text-gray-400'}`}
            >
              {done ? '✓' : active && !isTerminal ? (
                <span className="w-3 h-3 border-2 border-white border-t-transparent rounded-full animate-spin block" />
              ) : i + 1}
            </div>
            {/* Label */}
            <span className={`mt-1.5 text-center text-[10px] leading-tight font-medium
              ${active ? 'text-[#1e3a5f]' : done ? 'text-gray-600' : 'text-gray-400'}`}>
              {label}
            </span>
          </div>
        );
      })}
    </div>
  );
}

// ── Detail row ────────────────────────────────────────────────────────────────
function Detail({ label, value, mono, error }) {
  return (
    <>
      <dt className="text-xs font-semibold text-gray-400 uppercase tracking-wide py-1.5">{label}</dt>
      <dd className={`text-sm py-1.5 break-all ${mono ? 'font-mono text-xs' : ''} ${error ? 'text-red-600' : 'text-gray-800'}`}>{value}</dd>
    </>
  );
}

// ── Main component ────────────────────────────────────────────────────────────
export default function DocumentStatusTracker({ documentId, intervalMs = 4000 }) {
  const { document, status, loading, error, isTerminal, connectionState, refresh } =
    useDocumentStatus(documentId, intervalMs);

  if (!documentId) return <p className="text-center text-gray-400 py-8">No document ID provided.</p>;

  if (loading) {
    return (
      <div className="flex flex-col items-center py-12 text-gray-500">
        <div className="w-9 h-9 border-4 border-gray-200 border-t-[#1e3a5f] rounded-full animate-spin" />
        <p className="mt-3 text-sm">Fetching document status…</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-start gap-2 bg-red-50 text-red-800 border border-red-200 rounded-xl px-4 py-3 text-sm">
        <span>⚠️</span>
        <span className="flex-1">{error}</span>
        <button onClick={refresh} className="shrink-0 border border-red-300 text-red-700 px-2 py-0.5 rounded text-xs hover:bg-red-100 transition-colors">Retry</button>
      </div>
    );
  }

  if (!document) return null;

  const meta = STATUS_META[status] ?? STATUS_META.Pending;

  return (
    <div className="space-y-4">
      {/* Connection + refresh bar */}
      <div className="flex items-center justify-between">
        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold ring-1 ring-inset ${CONN_PILL[connectionState] ?? CONN_PILL[CONNECTION_STATE.Disconnected]}`}>
          {CONN_LABEL[connectionState] ?? '○ Polling'}
        </span>
        <button onClick={refresh} className="text-xs text-gray-500 hover:text-gray-800 border border-gray-200 px-2.5 py-1 rounded-lg hover:bg-gray-50 transition-colors">
          ↻ Refresh
        </button>
      </div>

      {/* Pipeline */}
      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5">
        <PipelineSteps currentStep={meta.step} isTerminal={isTerminal} status={status} />

        {/* Status header */}
        <div className={`flex items-start gap-3 rounded-xl p-4 mb-4 ${meta.bg}`}>
          <span className="text-2xl leading-none mt-0.5">{meta.icon}</span>
          <div>
            <div className="flex items-center gap-2 mb-0.5">
              <DocumentStatusBadge status={status} />
              {!isTerminal && <span className="text-xs text-gray-500 animate-pulse">updating…</span>}
            </div>
            <p className="text-sm text-gray-600">{meta.desc}</p>
          </div>
        </div>

        {/* Details */}
        <dl className="grid grid-cols-[max-content_1fr] gap-x-6 border-t border-gray-100 pt-3">
          <Detail label="Document ID"  value={document.id}           mono />
          <Detail label="File"         value={document.fileName} />
          <Detail label="Type"         value={document.documentType} />
          <Detail label="Size"         value={`${(document.fileSizeBytes / 1024).toFixed(1)} KB`} />
          <Detail label="Uploaded"     value={new Date(document.createdAtUtc).toLocaleString()} />
          <Detail label="Last updated" value={new Date(document.updatedAtUtc).toLocaleString()} />
          {document.failureReason && <Detail label="Failure reason" value={document.failureReason} error />}
          {document.remarks        && <Detail label="Remarks"       value={document.remarks} />}
        </dl>

        {/* Footer */}
        <div className="mt-4 pt-3 border-t border-gray-100 text-xs text-gray-400 text-right">
          {isTerminal ? '✓ Status is final — polling stopped' : '🔄 Auto-refreshing'}
        </div>
      </div>
    </div>
  );
}
