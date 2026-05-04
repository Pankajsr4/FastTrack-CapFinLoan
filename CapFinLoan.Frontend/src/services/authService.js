import { authApi } from './axiosInstances';

// All routes prefixed with /gateway/auth — Ocelot strips the prefix and forwards to AuthService

export const login = (email, password) =>
  authApi.post('/gateway/auth/login', { email, password });

export const register = ({ firstName, lastName, email, password, role, phone }) => {
  const name     = `${firstName} ${lastName}`.trim();
  const endpoint = role === 'Admin' ? '/gateway/auth/signup-admin' : '/gateway/auth/signup';
  return authApi.post(endpoint, { name, email, password, phone: phone || '0000000000' });
};
