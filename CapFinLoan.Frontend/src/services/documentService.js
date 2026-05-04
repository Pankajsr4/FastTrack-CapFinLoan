import { documentApi } from './axiosInstances';

// All routes prefixed with /gateway/documents — Ocelot forwards to DocumentService

export const getDocumentsByUser = (userId) =>
  documentApi.get(`/gateway/documents/user/${userId}`);

export const getDocumentsByApplication = (applicationId) =>
  documentApi.get(`/gateway/documents/application/${applicationId}`);

export const getDocumentById = (documentId) =>
  documentApi.get(`/gateway/documents/${documentId}`);

export const uploadDocument = (applicationId, documentType, file, onProgress) => {
  const formData = new FormData();
  formData.append('applicationId', applicationId);
  formData.append('documentType', documentType);
  formData.append('file', file);

  return documentApi.post('/gateway/documents/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    onUploadProgress: onProgress
      ? (e) => onProgress(Math.round((e.loaded * 100) / (e.total ?? e.loaded)))
      : undefined,
  });
};

export const replaceDocument = (documentId, file, documentType) => {
  const formData = new FormData();
  formData.append('file', file);
  if (documentType) formData.append('documentType', documentType);

  return documentApi.put(`/gateway/documents/${documentId}/replace`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
};
