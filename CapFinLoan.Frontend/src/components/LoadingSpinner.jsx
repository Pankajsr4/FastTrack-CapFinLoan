import { Loader2 } from 'lucide-react';

export default function LoadingSpinner({ message = 'Loading…' }) {
  return (
    <div className="flex flex-col items-center justify-center py-12 text-gray-500 fade-in">
      <Loader2 size={32} className="animate-spin text-[#1e3a5f]" />
      <p className="mt-3 text-sm">{message}</p>
    </div>
  );
}
