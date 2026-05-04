/**
 * Skeleton loading placeholder.
 * Pass className to control width/height/shape.
 *
 * Examples:
 *   <Skeleton className="h-4 w-32" />           — text line
 *   <Skeleton className="h-10 w-full" />         — input field
 *   <Skeleton className="h-24 w-24 rounded-full" /> — avatar
 */
export function Skeleton({ className = '' }) {
  return <div className={`skeleton ${className}`} aria-hidden="true" />;
}

/** Pre-built skeleton for a stat card */
export function StatCardSkeleton() {
  return (
    <div className="rounded-2xl border border-gray-100 bg-white p-5 flex items-center gap-4">
      <Skeleton className="w-12 h-12 rounded-xl shrink-0" />
      <div className="flex-1 space-y-2">
        <Skeleton className="h-6 w-16" />
        <Skeleton className="h-3 w-24" />
      </div>
    </div>
  );
}

/** Pre-built skeleton for a table row */
export function TableRowSkeleton({ cols = 5 }) {
  return (
    <tr>
      {Array.from({ length: cols }).map((_, i) => (
        <td key={i} className="px-4 py-3">
          <Skeleton className={`h-4 ${i === 0 ? 'w-36' : i === cols - 1 ? 'w-12' : 'w-20'}`} />
        </td>
      ))}
    </tr>
  );
}

/** Pre-built skeleton for a card block */
export function CardSkeleton({ lines = 3 }) {
  return (
    <div className="bg-white rounded-2xl border border-gray-200 p-5 space-y-3">
      <Skeleton className="h-5 w-40" />
      {Array.from({ length: lines }).map((_, i) => (
        <Skeleton key={i} className={`h-3 ${i === lines - 1 ? 'w-2/3' : 'w-full'}`} />
      ))}
    </div>
  );
}
