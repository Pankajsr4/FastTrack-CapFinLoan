import { createContext, useContext, useState } from 'react';

const AuthContext = createContext(null);

const TOKEN_KEY = 'token';
const ROLE_KEY  = 'role';

// ── Helpers ───────────────────────────────────────────────────────────────────

function decodePayload(token) {
  try {
    const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(atob(base64));
  } catch {
    return null;
  }
}

function isTokenValid(token) {
  if (!token) return false;
  const p = decodePayload(token);
  if (!p?.exp) return false;
  return p.exp * 1000 > Date.now() + 30_000;
}

// ASP.NET Core serialises ClaimTypes.Role as the full schema URL
const ROLE_CLAIM  = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const NAME_CLAIM  = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const EMAIL_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress';

function getUserFromToken(token) {
  const p = decodePayload(token);
  if (!p) return null;
  return {
    sub:   p.sub   ?? '',
    email: p.email ?? p[EMAIL_CLAIM] ?? '',
    name:  p.name  ?? p[NAME_CLAIM]  ?? '',
    role:  (p[ROLE_CLAIM] ?? p.role ?? '').toUpperCase(),
  };
}

// ── Storage helpers — use localStorage so it survives page reloads ────────────
const store = {
  get:    (k)    => localStorage.getItem(k),
  set:    (k, v) => localStorage.setItem(k, v),
  remove: (k)    => localStorage.removeItem(k),
};

// ── Provider ──────────────────────────────────────────────────────────────────

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => {
    const t = store.get(TOKEN_KEY);
    return isTokenValid(t) ? t : null;
  });

  const user = token ? getUserFromToken(token) : null;
  const isAuthenticated = !!token;

  /**
   * Always pass apiRole from the login/signup API response.
   * Never rely solely on JWT decode — ASP.NET uses long claim URL keys.
   */
  const saveToken = (newToken, apiRole) => {
    const jwtUser      = getUserFromToken(newToken);
    const resolvedRole = (apiRole ?? jwtUser?.role ?? '').toUpperCase();

    store.set(TOKEN_KEY, newToken);
    store.set(ROLE_KEY,  resolvedRole);

    console.log('[AuthContext] saveToken →', { resolvedRole, jwtRole: jwtUser?.role, apiRole });
    setToken(newToken);
  };

  const clearToken = () => {
    store.remove(TOKEN_KEY);
    store.remove(ROLE_KEY);
    console.log('[AuthContext] clearToken → cleared');
    setToken(null);
  };

  return (
    <AuthContext.Provider value={{ token, user, isAuthenticated, saveToken, clearToken }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuthContext() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuthContext must be used inside <AuthProvider>');
  return ctx;
}
