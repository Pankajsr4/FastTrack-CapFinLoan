import { adminApi } from './axiosInstances';

const BASE = '/gateway/admin';

export const getAdminApplications    = ()           => adminApi.get(`${BASE}/applications`);
export const getAdminApplicationById = (id)         => adminApi.get(`${BASE}/applications/${id}`);
export const reviewApplication       = (id, data)   => adminApi.put(`${BASE}/applications/${id}/status`, data);
export const disburseApplication     = (id, data)   => adminApi.post(`${BASE}/applications/${id}/disburse`, data);
export const getAdminDocuments       = (appId)      => adminApi.get(`${BASE}/documents/application/${appId}`);
export const verifyDocument          = (id, data)   => adminApi.put(`${BASE}/documents/${id}/verify`, data);
export const getDashboardAnalytics   = ()           => adminApi.get(`${BASE}/applications/dashboard`);
export const syncApplications        = ()           => adminApi.post(`${BASE}/sync/applications`);
