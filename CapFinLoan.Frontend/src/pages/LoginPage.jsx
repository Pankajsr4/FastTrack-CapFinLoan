import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { login as loginService } from '../services/authService';
import { useAuthContext } from '../context/AuthContext';

export default function LoginPage() {
  const [email,    setEmail]    = useState('');
  const [password, setPassword] = useState('');
  const [error,    setError]    = useState('');
  const [loading,  setLoading]  = useState(false);

  const { saveToken } = useAuthContext();
  const navigate      = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const res  = await loginService(email.trim(), password);
      const data = res.data;

      if (!data?.token) {
        setError('No token in response.');
        setLoading(false);
        return;
      }

      // Save via AuthContext so all hooks (useNotifications etc.) pick it up
      saveToken(data.token, data.role);

      const dest = (data.role || '').toUpperCase() === 'ADMIN'
        ? '/admin/dashboard'
        : '/applicant/dashboard';

      navigate(dest, { replace: true });

    } catch (err) {
      const msg = err?.response?.data?.message || err?.message || 'Login failed.';
      setError(msg);
      setLoading(false);
    }
  };

  return (
    <div className="min-h-[80vh] flex items-center justify-center px-4 bg-gray-50">
      <div className="w-full max-w-sm bg-white rounded-2xl shadow-lg p-8">
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-[#1e3a5f] text-white mb-3 text-xl">📄</div>
          <h1 className="text-2xl font-bold text-gray-900">Sign in</h1>
          <p className="text-sm text-gray-500 mt-1">CapFinLoan Document Portal</p>
        </div>

        {error && (
          <div className="bg-red-50 text-red-700 border border-red-200 rounded-lg px-4 py-3 text-sm mb-4">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label htmlFor="login-email" className="block text-sm font-medium text-gray-700 mb-1">Email</label>
            <input
              id="login-email"
              type="email" value={email} onChange={e => setEmail(e.target.value)}
              required placeholder="you@example.com" autoComplete="email"
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label htmlFor="login-password" className="block text-sm font-medium text-gray-700 mb-1">Password</label>
            <input
              id="login-password"
              type="password" value={password} onChange={e => setPassword(e.target.value)}
              required placeholder="••••••••" autoComplete="current-password"
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <button type="submit" disabled={loading}
            className="w-full py-2.5 bg-[#1e3a5f] hover:bg-[#0f2744] text-white font-semibold rounded-lg text-sm transition-colors disabled:opacity-60">
            {loading ? 'Signing in…' : 'Sign In'}
          </button>
        </form>

        <p className="text-center text-sm text-gray-500 mt-6">
          Don't have an account?{' '}
          <Link to="/signup" className="text-blue-600 hover:text-blue-800 font-medium">Create one</Link>
        </p>
      </div>
    </div>
  );
}
