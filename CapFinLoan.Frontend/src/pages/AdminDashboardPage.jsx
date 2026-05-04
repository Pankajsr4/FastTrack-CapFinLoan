import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import {
  BarChart, Bar, LineChart, Line, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer
} from 'recharts';
import {
  ClipboardList, CheckCircle, XCircle, Clock,
  FileSearch, IndianRupee, TrendingUp, RefreshCw
} from 'lucide-react';
import { useAdminDashboard } from '../hooks/useAdminDashboard';
import { getAdminApplications } from '../services/adminService';

// ── Colour palette ────────────────────────────────────────────────────────────
const COLOURS = {
  submitted: '#3b82f6',
  approved:  '#22c55e',
  rejected:  '#ef4444',
  disbursed: '#8b5cf6',
  pending:   '#f59e0b',
};

const PIE_COLOURS = [
  '#3b82f6', '#f59e0b', '#8b5cf6', '#06b6d4',
  '#22c55e', '#ef4444', '#a855f7',
];

// ── Helpers ───────────────────────────────────────────────────────────────────
function fmt(n)   { return Number(n ?? 0).toLocaleString('en-IN'); }
function fmtAmt(n){ return `₹${Number(n ?? 0).toLocaleString('en-IN', { maximumFractionDigits: 0 })}`; }

function StatCard({ label, value, Icon, colour, sub }) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 flex flex-col gap-1">
      <div className="flex items-center justify-between">
        <span className="text-xs text-gray-500 font-medium">{label}</span>
        <Icon size={16} className={colour} />
      </div>
      <p className={`text-2xl font-bold ${colour}`}>{value}</p>
      {sub && <p className="text-xs text-gray-400">{sub}</p>}
    </div>
  );
}

function SectionTitle({ children }) {
  return <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wide">{children}</h2>;
}

// ── Custom tooltip for charts ─────────────────────────────────────────────────
function ChartTooltip({ active, payload, label }) {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-lg p-3 text-xs">
      <p className="font-semibold text-gray-700 mb-1">{label}</p>
      {payload.map(p => (
        <p key={p.name} style={{ color: p.color }}>
          {p.name}: <span className="font-bold">{fmt(p.value)}</span>
        </p>
      ))}
    </div>
  );
}

// ── Status distribution for pie chart ────────────────────────────────────────
function buildPieData(d) {
  return [
    { name: 'Submitted',    value: d.submittedCount    ?? 0 },
    { name: 'Docs Pending', value: d.docsPendingCount  ?? 0 },
    { name: 'Docs Verified',value: d.docsVerifiedCount ?? 0 },
    { name: 'Under Review', value: d.underReviewCount  ?? 0 },
    { name: 'Approved',     value: d.approvedCount     ?? 0 },
    { name: 'Rejected',     value: d.rejectedCount     ?? 0 },
    { name: 'Disbursed',    value: d.disbursedCount    ?? 0 },
  ].filter(x => x.value > 0);
}

// ── Skeleton loader ───────────────────────────────────────────────────────────
function DashboardSkeleton() {
  return (
    <div className="space-y-6 animate-pulse">
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        {[...Array(8)].map((_, i) => (
          <div key={i} className="h-24 bg-gray-100 rounded-xl" />
        ))}
      </div>
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="h-72 bg-gray-100 rounded-xl" />
        <div className="h-72 bg-gray-100 rounded-xl" />
      </div>
      <div className="h-72 bg-gray-100 rounded-xl" />
    </div>
  );
}

// ── Recent applications table (separate fetch) ────────────────────────────────
function RecentApplications({ refreshKey }) {
  const [apps,    setApps]    = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    getAdminApplications()
      .then(r => setApps(r.data ?? []))   // no slice — show all
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [refreshKey]); // re-fetches whenever parent hits Refresh

  const STATUS_COLOUR = {
    Draft:          'bg-gray-100 text-gray-600',
    Submitted:      'bg-blue-100 text-blue-700',
    'Docs Pending': 'bg-yellow-100 text-yellow-700',
    'Docs Verified':'bg-teal-100 text-teal-700',
    'Under Review': 'bg-purple-100 text-purple-700',
    Approved:       'bg-green-100 text-green-700',
    Rejected:       'bg-red-100 text-red-700',
    Disbursed:      'bg-violet-100 text-violet-700',
  };

  return (
    <div className="bg-white rounded-2xl border border-gray-200 shadow-sm overflow-hidden">
      <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <SectionTitle>Recent Applications</SectionTitle>
          {apps.length > 0 && (
            <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full font-medium">
              {apps.length}
            </span>
          )}
        </div>
        <Link to="/admin/applications" className="text-xs text-blue-600 hover:underline font-medium">
          View all →
        </Link>
      </div>
      {loading ? (
        <div className="p-6 text-center text-sm text-gray-400">Loading…</div>
      ) : (
        <div className="overflow-x-auto max-h-96 overflow-y-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-100">
              <tr>
                {['App #', 'Applicant', 'Amount', 'Status', 'Submitted', ''].map(h => (
                  <th key={h} className="text-left text-xs font-semibold text-gray-500 uppercase tracking-wide px-4 py-3">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {apps.length === 0 && (
                <tr><td colSpan={6} className="text-center text-gray-400 py-6 text-sm">No applications</td></tr>
              )}
              {apps.map(app => (
                <tr key={app.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3 font-mono text-xs text-gray-600">{app.applicationNumber}</td>
                  <td className="px-4 py-3">
                    <p className="font-medium text-gray-900 text-xs">
                      {app.personalDetails
                        ? `${app.personalDetails.firstName ?? ''} ${app.personalDetails.lastName ?? ''}`.trim()
                        : `${app.firstName ?? ''} ${app.lastName ?? ''}`.trim() || app.applicantName || '—'}
                    </p>
                    <p className="text-[11px] text-gray-400">{app.personalDetails?.email ?? app.email ?? ''}</p>
                  </td>
                  <td className="px-4 py-3 text-xs text-gray-700">
                    ₹{Number(app.loanDetails?.requestedAmount ?? app.requestedAmount ?? 0).toLocaleString()}
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
                      className="text-xs text-blue-600 hover:underline font-medium">Review →</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────
export default function AdminDashboardPage() {
  const { data, loading, error, lastFetchedAt, refetch } = useAdminDashboard();
  const [refreshKey, setRefreshKey] = useState(0);

  const handleRefresh = () => {
    refetch();
    setRefreshKey(k => k + 1); // triggers RecentApplications re-fetch
  };

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-8">
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold text-gray-900">Admin Dashboard</h1>
        </div>
        <DashboardSkeleton />
      </div>
    );
  }

  if (error) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-8">
        <h1 className="text-2xl font-bold text-gray-900 mb-4">Admin Dashboard</h1>
        <div className="bg-red-50 border border-red-200 rounded-xl p-6 text-center">
          <p className="text-red-600 text-sm mb-3">{error}</p>
          <button onClick={refetch}
            className="inline-flex items-center gap-2 text-xs bg-red-600 text-white px-4 py-2 rounded-lg hover:bg-red-700 transition-colors">
            <RefreshCw size={13} /> Retry
          </button>
        </div>
      </div>
    );
  }

  const d = data ?? {};
  const monthly = (d.monthlyStats ?? []).map(m => ({
    ...m,
    // Shorten label: "2026-04" → "Apr"
    label: new Date(`${m.month}-01`).toLocaleString('default', { month: 'short', year: '2-digit' })
  }));

  const pieData = buildPieData(d);

  return (
    <div className="max-w-7xl mx-auto px-4 py-8 space-y-8">

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Admin Dashboard</h1>
          {lastFetchedAt && (
            <p className="text-xs text-gray-400 mt-0.5">
              Last updated {lastFetchedAt.toLocaleTimeString()}
            </p>
          )}
        </div>
        <button onClick={handleRefresh}
          className="flex items-center gap-1.5 text-xs border border-gray-300 text-gray-600 hover:bg-gray-50 px-3 py-1.5 rounded-lg transition-colors">
          <RefreshCw size={13} /> Refresh
        </button>
      </div>

      {/* ── KPI cards ── */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <StatCard label="Total Applications" value={fmt(d.totalApplications)}
          Icon={ClipboardList} colour="text-blue-600" />
        <StatCard label="Pending Review"     value={fmt(d.pendingCount)}
          Icon={Clock}         colour="text-amber-500" />
        <StatCard label="Approved"           value={fmt(d.approvedCount)}
          Icon={CheckCircle}   colour="text-green-600" />
        <StatCard label="Rejected"           value={fmt(d.rejectedCount)}
          Icon={XCircle}       colour="text-red-500" />
        <StatCard label="Under Review"       value={fmt(d.underReviewCount)}
          Icon={FileSearch}    colour="text-purple-600" />
        <StatCard label="Disbursed"          value={fmt(d.disbursedCount)}
          Icon={TrendingUp}    colour="text-violet-600" />
        <StatCard label="Total Requested"    value={fmtAmt(d.totalRequestedAmount)}
          Icon={IndianRupee}   colour="text-blue-500"
          sub="All submitted applications" />
        <StatCard label="Total Disbursed"    value={fmtAmt(d.totalDisbursedAmount)}
          Icon={IndianRupee}   colour="text-green-500"
          sub="Disbursed loan amount" />
      </div>

      {/* ── Charts row ── */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">

        {/* Monthly bar chart */}
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5">
          <SectionTitle>Monthly Applications (Last 12 Months)</SectionTitle>
          <div className="mt-4 h-64">
            {monthly.length === 0 ? (
              <div className="h-full flex items-center justify-center text-sm text-gray-400">No data yet</div>
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={monthly} margin={{ top: 4, right: 8, left: -20, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                  <Tooltip content={<ChartTooltip />} />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                  <Bar dataKey="submitted" name="Submitted" fill={COLOURS.submitted} radius={[3,3,0,0]} />
                  <Bar dataKey="approved"  name="Approved"  fill={COLOURS.approved}  radius={[3,3,0,0]} />
                  <Bar dataKey="rejected"  name="Rejected"  fill={COLOURS.rejected}  radius={[3,3,0,0]} />
                  <Bar dataKey="disbursed" name="Disbursed" fill={COLOURS.disbursed} radius={[3,3,0,0]} />
                </BarChart>
              </ResponsiveContainer>
            )}
          </div>
        </div>

        {/* Status distribution pie chart */}
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5">
          <SectionTitle>Status Distribution</SectionTitle>
          <div className="mt-4 h-64">
            {pieData.length === 0 ? (
              <div className="h-full flex items-center justify-center text-sm text-gray-400">No data yet</div>
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie
                    data={pieData}
                    cx="50%"
                    cy="50%"
                    innerRadius={55}
                    outerRadius={90}
                    paddingAngle={3}
                    dataKey="value"
                    label={({ name, percent }) =>
                      percent > 0.04 ? `${name} ${(percent * 100).toFixed(0)}%` : ''
                    }
                    labelLine={false}
                  >
                    {pieData.map((_, i) => (
                      <Cell key={i} fill={PIE_COLOURS[i % PIE_COLOURS.length]} />
                    ))}
                  </Pie>
                  <Tooltip formatter={(v) => [fmt(v), 'Applications']} />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                </PieChart>
              </ResponsiveContainer>
            )}
          </div>
        </div>
      </div>

      {/* Monthly trend line chart */}
      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5">
        <SectionTitle>Monthly Loan Amount Trend</SectionTitle>
        <div className="mt-4 h-56">
          {monthly.length === 0 ? (
            <div className="h-full flex items-center justify-center text-sm text-gray-400">No data yet</div>
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={monthly} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis
                  tick={{ fontSize: 11 }}
                  tickFormatter={v => v >= 100000 ? `₹${(v/100000).toFixed(1)}L` : `₹${(v/1000).toFixed(0)}K`}
                />
                <Tooltip
                  formatter={(v) => [fmtAmt(v), 'Total Amount']}
                  content={({ active, payload, label }) => {
                    if (!active || !payload?.length) return null;
                    return (
                      <div className="bg-white border border-gray-200 rounded-lg shadow-lg p-3 text-xs">
                        <p className="font-semibold text-gray-700 mb-1">{label}</p>
                        <p style={{ color: '#3b82f6' }}>
                          Total Amount: <span className="font-bold">{fmtAmt(payload[0]?.value)}</span>
                        </p>
                      </div>
                    );
                  }}
                />
                <Line
                  type="monotone"
                  dataKey="totalAmount"
                  name="Total Amount"
                  stroke="#3b82f6"
                  strokeWidth={2}
                  dot={{ r: 3, fill: '#3b82f6' }}
                  activeDot={{ r: 5 }}
                />
              </LineChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>

      {/* Recent applications table */}
      <RecentApplications refreshKey={refreshKey} />

    </div>
  );
}
