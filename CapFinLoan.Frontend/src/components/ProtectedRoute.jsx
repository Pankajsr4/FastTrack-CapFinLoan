import { Navigate, Outlet, useLocation } from 'react-router-dom';

export default function ProtectedRoute({ role: requiredRole }) {
  const location = useLocation();

  const token         = localStorage.getItem('token');
  const storedRole    = (localStorage.getItem('role') ?? '').toUpperCase();
  const requiredUpper = (requiredRole ?? '').toUpperCase();

  console.log('[ProtectedRoute]', {
    path: location.pathname,
    hasToken: !!token,
    storedRole,
    requiredRole: requiredUpper || 'any',
  });

  if (!token) {
    console.warn('[ProtectedRoute] no token → /login');
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (requiredUpper && storedRole !== requiredUpper) {
    const fallback = storedRole === 'ADMIN' ? '/admin/dashboard' : '/applicant/dashboard';
    console.warn(`[ProtectedRoute] role mismatch: need "${requiredUpper}", have "${storedRole}" → ${fallback}`);
    return <Navigate to={fallback} replace />;
  }

  console.log('[ProtectedRoute] ✓ access granted');
  return <Outlet />;
}
