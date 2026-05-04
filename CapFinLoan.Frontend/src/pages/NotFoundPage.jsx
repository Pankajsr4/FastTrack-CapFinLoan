import { Link } from 'react-router-dom';
import { FileQuestion, ArrowLeft } from 'lucide-react';

export default function NotFoundPage() {
  return (
    <div className="flex flex-col items-center justify-center min-h-[70vh] text-center px-4 fade-in">
      <FileQuestion size={64} className="text-gray-200 mb-4" />
      <h1 className="text-6xl font-black text-gray-200 mb-2">404</h1>
      <h2 className="text-xl font-bold text-gray-800 mb-2">Page not found</h2>
      <p className="text-gray-500 mb-6 text-sm">The page you're looking for doesn't exist.</p>
      <Link to="/documents" className="inline-flex items-center gap-2 px-5 py-2 bg-[#1e3a5f] text-white text-sm font-semibold rounded-lg hover:bg-[#0f2744] transition-colors">
        <ArrowLeft size={14} /> Back to Documents
      </Link>
    </div>
  );
}
