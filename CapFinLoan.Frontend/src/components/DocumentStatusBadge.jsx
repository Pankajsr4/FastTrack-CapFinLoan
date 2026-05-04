const BADGE = {
  Pending:          'bg-yellow-100 text-yellow-800 ring-yellow-300',
  Processing:       'bg-blue-100   text-blue-800   ring-blue-300',
  Completed:        'bg-green-100  text-green-800  ring-green-300',
  UnderReview:      'bg-purple-100 text-purple-800 ring-purple-300',
  Verified:         'bg-green-100  text-green-800  ring-green-300',
  ReuploadRequired: 'bg-red-100    text-red-800    ring-red-300',
  Failed:           'bg-red-100    text-red-800    ring-red-300',
};

const ICON = {
  Pending:          '🟡',
  Processing:       '🔵',
  Completed:        '🟢',
  UnderReview:      '🔍',
  Verified:         '✅',
  ReuploadRequired: '🔄',
  Failed:           '🔴',
};

export default function DocumentStatusBadge({ status }) {
  const cls  = BADGE[status] ?? 'bg-gray-100 text-gray-700 ring-gray-300';
  const icon = ICON[status]  ?? '⚪';
  return (
    <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ring-1 ring-inset ${cls}`}>
      <span aria-hidden="true">{icon}</span>
      {status}
    </span>
  );
}
