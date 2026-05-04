export default function DebugPage() {
  const token = localStorage.getItem('token');
  const role  = localStorage.getItem('role');

  const payload = (() => {
    if (!token) return null;
    try {
      const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      return JSON.parse(atob(base64));
    } catch { return 'decode failed'; }
  })();

  return (
    <div style={{ fontFamily: 'monospace', padding: 32, maxWidth: 900 }}>
      <h2 style={{ marginBottom: 16 }}>🔍 Auth Debug (localStorage)</h2>

      <table border="1" cellPadding="8" style={{ borderCollapse: 'collapse', width: '100%', marginBottom: 24 }}>
        <tbody>
          <tr><td><b>localStorage.token</b></td><td style={{ wordBreak: 'break-all' }}>{token ?? '❌ NOT SET'}</td></tr>
          <tr><td><b>localStorage.role</b></td><td>{role ?? '❌ NOT SET'}</td></tr>
        </tbody>
      </table>

      <h3>JWT Payload Claims:</h3>
      <pre style={{ background: '#f4f4f4', padding: 16, overflow: 'auto' }}>
        {payload ? JSON.stringify(payload, null, 2) : '(no token)'}
      </pre>

      <div style={{ marginTop: 24, display: 'flex', gap: 12, flexWrap: 'wrap' }}>
        <a href="/applicant/dashboard" style={{ padding: '8px 16px', background: '#1e3a5f', color: 'white', borderRadius: 6, textDecoration: 'none' }}>
          → /applicant/dashboard
        </a>
        <a href="/admin/dashboard" style={{ padding: '8px 16px', background: '#1e3a5f', color: 'white', borderRadius: 6, textDecoration: 'none' }}>
          → /admin/dashboard
        </a>
        <button onClick={() => { localStorage.clear(); window.location.reload(); }}
          style={{ padding: '8px 16px', background: '#dc2626', color: 'white', borderRadius: 6, border: 'none', cursor: 'pointer' }}>
          Clear localStorage
        </button>
      </div>
    </div>
  );
}
