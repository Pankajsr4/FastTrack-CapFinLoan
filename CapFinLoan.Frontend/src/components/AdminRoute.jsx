import { Navigate, Outlet, useLocation } from 'react-router-dom';

export default function AdminRoute() {
  const location = useLocation();
  const token = localStorage.getItem('token');
  const role  = (localStorage.getItem('role') ?? '').toUpperCase();

  if (!token)
    return <Navigate to="/login" state={{ from: location }} replace />;

  if (role !== 'ADMIN')
    return <Navigate to="/applicant/dashboard" replace />;

  return <Outlet />;
}
