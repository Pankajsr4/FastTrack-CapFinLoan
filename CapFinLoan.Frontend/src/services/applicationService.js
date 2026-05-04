import { applicationApi } from './axiosInstances';

const BASE = '/gateway/applications';

export const getMyApplications    = ()           => applicationApi.get(`${BASE}/my`);
export const getApplicationById   = (id)         => applicationApi.get(`${BASE}/${id}`);
export const getApplicationStatus = (id)         => applicationApi.get(`${BASE}/${id}/status`);
export const createApplication    = (data)       => applicationApi.post(BASE, data);
export const updateApplication    = (id, data)   => applicationApi.put(`${BASE}/${id}`, data);
export const submitApplication    = (id)         => applicationApi.post(`${BASE}/${id}/submit`);
export const withdrawApplication  = (id, reason) => applicationApi.post(`${BASE}/${id}/withdraw`, { reason: reason ?? null });
export const deleteApplication    = (id)         => applicationApi.delete(`${BASE}/${id}`);
