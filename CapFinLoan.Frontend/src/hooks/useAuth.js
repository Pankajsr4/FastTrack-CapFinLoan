import { useState } from 'react';
import { login, register } from '../services/authService';
import { useAuthContext } from '../context/AuthContext';

export function useAuth() {
  const { saveToken, clearToken } = useAuthContext();
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState(null);

  const signIn = async (email, password) => {
    setLoading(true);
    setError(null);
    try {
      console.log('[useAuth] signIn → calling API for:', email);
      const { data } = await login(email, password);

      console.log('[useAuth] signIn → raw API response:', JSON.stringify(data));
      console.log('[useAuth] signIn → data.token exists:', !!data.token);
      console.log('[useAuth] signIn → data.role:', data.role);

      if (!data.token) {
        throw new Error('No token in response');
      }

      // Pass role from API response directly — do NOT rely on JWT decode
      saveToken(data.token, data.role);

      const role = (data.role ?? '').toUpperCase();
      const destination = role === 'ADMIN' ? '/admin/dashboard' : '/applicant/dashboard';
      console.log('[useAuth] signIn → navigating to:', destination);

      window.location.href = destination;
    } catch (err) {
      console.error('[useAuth] signIn → error:', err.response?.status, err.response?.data ?? err.message);
      setError(err.response?.data?.message ?? 'Login failed. Please check your credentials.');
      setLoading(false);
    }
  };

  const signUp = async (payload) => {
    setLoading(true);
    setError(null);
    try {
      console.log('[useAuth] signUp → calling API for:', payload.email, 'role:', payload.role);
      const { data } = await register(payload);

      console.log('[useAuth] signUp → raw API response:', JSON.stringify(data));
      console.log('[useAuth] signUp → data.token exists:', !!data.token);
      console.log('[useAuth] signUp → data.role:', data.role);

      if (data.token) {
        // Pass role from API response directly
        saveToken(data.token, data.role);

        const role = (data.role ?? '').toUpperCase();
        const destination = role === 'ADMIN' ? '/admin/dashboard' : '/applicant/dashboard';
        console.log('[useAuth] signUp → navigating to:', destination);

        window.location.href = destination;
      } else {
        console.warn('[useAuth] signUp → no token, redirecting to login');
        window.location.href = '/login?registered=1';
      }
    } catch (err) {
      console.error('[useAuth] signUp → error:', err.response?.status, err.response?.data ?? err.message);
      setError(err.response?.data?.message ?? 'Registration failed. Please try again.');
      setLoading(false);
    }
  };

  const signOut = () => {
    console.log('[useAuth] signOut → clearing session');
    clearToken();
    window.location.href = '/login';
  };

  return { signIn, signUp, signOut, loading, error, clearError: () => setError(null) };
}
