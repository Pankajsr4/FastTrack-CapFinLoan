import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import DocumentStatusTracker from '../components/DocumentStatusTracker';

export default function DocumentStatusPage() {
  const { id }   = useParams();
  const navigate = useNavigate();

  const [inputId,    setInputId]    = useState(id ?? '');
  const [trackingId, setTrackingId] = useState(id ?? '');

  const handleTrack = (e) => { e.preventDefault(); setTrackingId(inputId.trim()); };

  return (
    <div className="max-w-2xl mx-auto px-4 py-8">
      <div className="flex items-center gap-3 mb-6">
        <button onClick={() => navigate('/documents')} className="text-gray-500 hover:text-gray-800 text-sm transition-colors">← Documents</button>
        <h1 className="text-2xl font-bold text-gray-900">Document Status</h1>
      </div>

      <form onSubmit={handleTrack} className="flex gap-2 mb-8">
        <input
          value={inputId}
          onChange={(e) => setInputId(e.target.value)}
          placeholder="Paste Document ID (GUID)"
          className="flex-1 px-3 py-2 border border-gray-300 rounded-lg text-sm font-mono focus:outline-none focus:ring-2 focus:ring-blue-500"
          aria-label="Document ID"
        />
        <button type="submit" className="px-4 py-2 bg-[#1e3a5f] text-white text-sm font-semibold rounded-lg hover:bg-[#0f2744] transition-colors">
          Track
        </button>
      </form>

      {trackingId && <DocumentStatusTracker documentId={trackingId} />}
    </div>
  );
}
