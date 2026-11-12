import { Link } from 'react-router'
import type { RecentComparison } from '../history.ts'

/** Reports made in this browser, newest first. Renders nothing when there are none. */
export function RecentComparisons({ comparisons }: { comparisons: RecentComparison[] }) {
  if (comparisons.length === 0) return null

  return (
    <section className="mt-12 max-w-2xl">
      <h2 className="font-semibold">Recent comparisons</h2>
      <ul className="mt-2 divide-y divide-rule border-y border-rule">
        {comparisons.map((comparison) => (
          <li key={comparison.id}>
            <Link
              to={`/analyses/${comparison.id}`}
              className="flex items-baseline justify-between gap-4 py-3 hover:underline hover:underline-offset-4"
            >
              <span className="min-w-0 truncate font-medium">{comparison.title}</span>
              <span className="shrink-0 text-sm text-ink-soft tabular-nums">
                {comparison.score !== null && `${comparison.score} of 100, `}
                {new Date(comparison.createdAt).toLocaleDateString('en', { day: 'numeric', month: 'short' })}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  )
}
