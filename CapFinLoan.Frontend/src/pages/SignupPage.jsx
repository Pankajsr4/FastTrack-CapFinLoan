import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { register } from '../services/authService';
import { useAuthContext } from '../context/AuthContext';

export default function SignupPage() {
  const [form, setForm] = useState({
    firstName: '', lastName: '', email: '', phone: '', password: '', confirm: '', role: 'Applicant',
  });
  const [error,   setError]   = useState('');
  const [loading, setLoading] = useState(false);

  const { saveToken } = useAuthContext();
  const navigate      = useNavigate();

  const set = f => e => setForm(p => ({ ...p, [f]: e.target.value }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');

    if (form.password.length < 8) { setError('Password must be at least 8 characters.'); return; }
    if (form.password !== form.confirm) { setError('Passwords do not match.'); return; }

    setLoading(true);

    try {
      const res  = await register({
        firstName: form.firstName,
        lastName:  form.lastName,
        email:     form.email.trim(),
        password:  form.password,
        role:      form.role,
        phone:     form.phone,
      });
      const data = res.data;

      if (!data?.token) {
        setError('No token received from server.');
        setLoading(false);
        return;
      }

      saveToken(data.token, data.role);

      const dest = (data.role || '').toUpperCase() === 'ADMIN'
        ? '/admin/dashboard'
        : '/applicant/dashboard';

      navigate(dest, { replace: true });

    } catch (err) {
      const msg = err?.response?.data?.message || err?.message || 'Registration failed.';
      setError(msg);
      setLoading(false);
    }
  };

  return (
    <div className="min-h-[80vh] flex items-center justify-center px-4 bg-gray-50 py-8">
      <div className="w-full max-w-sm bg-white rounded-2xl shadow-lg p-8">
        <div className="text-center mb-6">
          <div className="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-[#1e3a5f] text-white text-xl mb-3">📄</div>
          <h1 className="text-2xl font-bold text-gray-900">Create account</h1>
          <p className="text-sm text-gray-500 mt-1">CapFinLoan Document Portal</p>
        </div>

        {error && (
          <div className="bg-red-50 text-red-700 border border-red-200 rounded-lg px-4 py-3 text-sm mb-4">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Role selector */}
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-2">I am a</label>
            <div className="grid grid-cols-2 gap-3">
              {[{value:'Applicant',icon:'👤'},{value:'Admin',icon:'🛡️'}].map(({value,icon}) => (
                <button key={value} type="button" onClick={() => setForm(f => ({...f, role: value}))}
                  className={`flex flex-col items-center gap-1 p-3 rounded-xl border-2 text-center transition-all
                    ${form.role === value ? 'border-[#1e3a5f] bg-blue-50 text-[#1e3a5f]' : 'border-gray-200 text-gray-500 hover:border-gray-300'}`}>
                  <span className="text-xl">{icon}</span>
                  <span className="text-xs font-semibold">{value}</span>
                </button>
              ))}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label htmlFor="signup-firstName" className="block text-sm font-medium text-gray-700 mb-1">First name</label>
              <input id="signup-firstName" value={form.firstName} onChange={set('firstName')} required placeholder="Jane"
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div>
              <label htmlFor="signup-lastName" className="block text-sm font-medium text-gray-700 mb-1">Last name</label>
              <input id="signup-lastName" value={form.lastName} onChange={set('lastName')} required placeholder="Doe"
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>

          <div>
            <label htmlFor="signup-email" className="block text-sm font-medium text-gray-700 mb-1">Email</label>
            <input id="signup-email" type="email" value={form.email} onChange={set('email')} required placeholder="you@example.com"
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>

          <div>
            <label htmlFor="signup-phone" className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
            <input id="signup-phone" type="tel" value={form.phone} onChange={set('phone')} placeholder="+1 555 000 0000"
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>

          <div>
            <label htmlFor="signup-password" className="block text-sm font-medium text-gray-700 mb-1">Password</label>
            <input id="signup-password" type="password" value={form.password} onChange={set('password')} required placeholder="Min. 8 chars"
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>

          <div>
            <label htmlFor="signup-confirm" className="block text-sm font-medium text-gray-700 mb-1">Confirm password</label>
            <input id="signup-confirm" type="password" value={form.confirm} onChange={set('confirm')} required placeholder="••••••••"
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>

          <button type="submit" disabled={loading}
            className="w-full py-2.5 bg-[#1e3a5f] hover:bg-[#0f2744] text-white font-semibold rounded-lg text-sm transition-colors disabled:opacity-60">
            {loading ? 'Creating account…' : 'Create Account'}
          </button>
        </form>

        <p className="text-center text-sm text-gray-500 mt-6">
          Already have an account?{' '}
          <Link to="/login" className="text-blue-600 hover:text-blue-800 font-medium">Sign in</Link>
        </p>
      </div>
    </div>
  );
}
