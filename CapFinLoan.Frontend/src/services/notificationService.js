import { applicationApi } from './axiosInstances';

const BASE = '/gateway/notifications';

export const getNotifications  = (userId) => applicationApi.get(`${BASE}/user/${userId}`);
export const markNotificationRead = (id)  => applicationApi.put(`${BASE}/${id}/read`);
