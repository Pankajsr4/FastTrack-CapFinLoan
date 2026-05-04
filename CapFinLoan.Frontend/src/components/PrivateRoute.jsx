import { Navigate, Outlet, useLocation } from 'react-router-dom';

export default function PrivateRoute() {
  const location = useLocation();
  const token = localStorage.getItem('token');
  return token
    ? <Outlet />
    : <Navigate to="/login" state={{ from: location }} replace />;
}
