import axios from 'axios';

const GATEWAY = import.meta.env.VITE_GATEWAY_URL ?? 'http://localhost:5000';

function createInstance(baseURL) {
  const instance = axios.create({
    baseURL,
    headers: { 'Content-Type': 'application/json' },
  });

  // Attach JWT from localStorage on every request
  instance.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
  });

  // NO automatic redirect on 401 — let each page handle errors itself
  // The 401 interceptor was causing the dashboard to kick users back to login

  return instance;
}

export const authApi        = createInstance(GATEWAY);
export const applicationApi = createInstance(GATEWAY);
export const documentApi    = createInstance(GATEWAY);
export const adminApi       = createInstance(GATEWAY);
