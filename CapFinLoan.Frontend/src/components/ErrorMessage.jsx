import { AlertCircle } from 'lucide-react';

export default function ErrorMessage({ message }) {
  if (!message) return null;
  return (
    <div className="flex items-start gap-2 bg-red-50 text-red-800 border border-red-200 rounded-lg px-4 py-3 text-sm my-3 fade-in">
      <AlertCircle size={16} className="shrink-0 mt-0.5" />
      <span>{message}</span>
    </div>
  );
}
