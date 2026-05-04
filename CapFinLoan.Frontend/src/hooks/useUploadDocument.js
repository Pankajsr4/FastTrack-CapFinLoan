import { useState } from 'react';
import { uploadDocument } from '../services/documentService';

export const UPLOAD_PHASE = {
  Idle:       'idle',
  Preparing:  'preparing',   // validating + building FormData
  Uploading:  'uploading',   // bytes in-flight
  Processing: 'processing',  // server received, waiting for response
  Done:       'done',
  Error:      'error',
};

/**
 * Manages the full document upload lifecycle with granular phase tracking.
 *
 * Returns:
 *   upload(applicationId, documentType, file)
 *   phase       — one of UPLOAD_PHASE values
 *   progress    — 0-100 (bytes sent percentage)
 *   documentId  — ID returned by the API on success
 *   error       — error message or null
 *   reset()     — clears all state back to Idle
 *   retry()     — re-runs the last upload attempt
 */
export function useUploadDocument() {
  const [phase,      setPhase]      = useState(UPLOAD_PHASE.Idle);
  const [progress,   setProgress]   = useState(0);
  const [documentId, setDocumentId] = useState(null);
  const [error,      setError]      = useState(null);

  // Keep last args so retry() can replay them
  const [lastArgs, setLastArgs] = useState(null);

  const _run = async (applicationId, documentType, file) => {
    setPhase(UPLOAD_PHASE.Preparing);
    setProgress(0);
    setDocumentId(null);
    setError(null);

    try {
      // Short pause so "Preparing" is visible (avoids flash)
      await new Promise((r) => setTimeout(r, 150));

      setPhase(UPLOAD_PHASE.Uploading);

      const { data } = await uploadDocument(
        applicationId,
        documentType,
        file,
        (pct) => {
          setProgress(pct);
          // Once bytes are fully sent, switch to "Processing" while server responds
          if (pct === 100) setPhase(UPLOAD_PHASE.Processing);
        }
      );

      setDocumentId(data.id);
      setProgress(100);
      setPhase(UPLOAD_PHASE.Done);
    } catch (err) {
      setError(err.response?.data?.message ?? 'Upload failed. Please try again.');
      setPhase(UPLOAD_PHASE.Error);
    }
  };

  const upload = (applicationId, documentType, file) => {
    setLastArgs({ applicationId, documentType, file });
    return _run(applicationId, documentType, file);
  };

  const retry = () => {
    if (lastArgs) return _run(lastArgs.applicationId, lastArgs.documentType, lastArgs.file);
  };

  const reset = () => {
    setPhase(UPLOAD_PHASE.Idle);
    setProgress(0);
    setDocumentId(null);
    setError(null);
    setLastArgs(null);
  };

  return {
    upload,
    retry,
    reset,
    phase,
    uploading:  phase === UPLOAD_PHASE.Uploading || phase === UPLOAD_PHASE.Preparing || phase === UPLOAD_PHASE.Processing,
    progress,
    documentId,
    error,
  };
}
