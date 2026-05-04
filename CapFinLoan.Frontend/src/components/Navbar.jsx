import { Link, useLocation } from 'react-router-dom';
import { LayoutDashboard, FolderOpen, Upload, LogOut, FileText, ClipboardList } from 'lucide-react';
import { useAuthContext } from '../context/AuthContext';
import { useAuth } from '../hooks/useAuth';
import NotificationBell from './NotificationBell';

const APPLICANT_LINKS = [
  { to: '/applicant/dashboard', label: 'Dashboard', Icon: LayoutDashboard },
  { to: '/applications',        label: 'My Loans',  Icon: ClipboardList   },
  { to: '/documents',           label: 'Documents', Icon: FolderOpen      },
  { to: '/documents/upload',    label: 'Upload',    Icon: Upload          },
];

const ADMIN_LINKS = [
  { to: '/admin/dashboard',     label: 'Dashboard',     Icon: LayoutDashboard },
  { to: '/admin/applications',  label: 'Applications',  Icon: ClipboardList   },
];

export default function Navbar() {
  const { isAuthenticated, user } = useAuthContext();
  const { signOut } = useAuth();
  const { pathname } = useLocation();

  const isAdmin = (user?.role ?? '').toUpperCase() === 'ADMIN';
  const links   = isAdmin ? ADMIN_LINKS : APPLICANT_LINKS;
  const homeUrl = isAdmin ? '/admin/dashboard' : '/applicant/dashboard';

  return (
    <nav className="bg-[#1e3a5f] text-white shadow-md">
      <div className="max-w-6xl mx-auto px-4 h-14 flex items-center justify-between">

        <Link to={homeUrl}
          className="flex items-center gap-2 text-white font-bold text-lg tracking-tight hover:text-blue-200 transition-colors">
          <FileText size={20} className="text-blue-300" />
          CapFinLoan
          {isAdmin && <span className="text-xs bg-blue-600 px-1.5 py-0.5 rounded font-semibold ml-1">ADMIN</span>}
        </Link>

        {isAuthenticated ? (
          <div className="flex items-center gap-1 text-sm">
            {links.map(({ to, label, Icon }) => {
              const active = pathname === to || (to !== homeUrl && pathname.startsWith(to));
              return (
                <Link key={to} to={to}
                  className={`hidden sm:flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors
                    ${active ? 'bg-white/15 text-white' : 'text-blue-200 hover:text-white hover:bg-white/10'}`}>
                  <Icon size={13} />{label}
                </Link>
              );
            })}
            {/* Notification bell — applicants only */}
            {!isAdmin && <NotificationBell />}

            {user?.name && (
              <span className="hidden lg:block text-blue-300 text-xs border-l border-blue-700 pl-4 ml-2 max-w-[140px] truncate">
                {user.name}
              </span>
            )}
            <button onClick={signOut}
              className="flex items-center gap-1.5 ml-2 border border-blue-400 text-blue-200 hover:bg-blue-800 hover:text-white px-3 py-1.5 rounded-lg transition-colors text-xs">
              <LogOut size={13} /> Sign out
            </button>
          </div>
        ) : (
          <div className="flex items-center gap-3 text-sm">
            <Link to="/login"  className="text-blue-200 hover:text-white transition-colors text-sm">Sign in</Link>
            <Link to="/signup" className="bg-blue-600 hover:bg-blue-500 text-white px-3 py-1.5 rounded-lg transition-colors text-xs font-semibold">
              Sign up
            </Link>
          </div>
        )}
      </div>
    </nav>
  );
}
