import { applicationApi } from './axiosInstances';

export const sendChatMessage = (message, applicationContext = null) =>
  applicationApi.post('/gateway/ai/chat', { message, applicationContext });
