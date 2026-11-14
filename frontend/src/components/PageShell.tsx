import type { ReactNode } from 'react'
import { Link } from 'react-router'

/** The frame every page shares: the product name linking home, and an optional action beside it. */
export function PageShell({ action, children }: { action?: ReactNode; children: ReactNode }) {
  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-8 sm:py-12">
      <nav className="flex min-h-10 items-center justify-between gap-4">
        <Link to="/" className="flex items-center gap-2.5 text-lg font-semibold">
          {/* Three lines of a document, the middle one highlighted: the product in one mark. */}
          <svg viewBox="0 0 20 20" className="h-5 w-5" aria-hidden="true">
            <rect x="1" y="2" width="18" height="3" fill="var(--color-ink)" />
            <rect x="1" y="7.5" width="18" height="5" fill="var(--color-marker-deep)" />
            <rect x="1" y="15" width="11" height="3" fill="var(--color-ink)" />
          </svg>
          ProofFit
        </Link>
        {action}
      </nav>
      {children}
    </div>
  )
}

export function Notice({ children }: { children: ReactNode }) {
  return <p className="rounded-sm border border-rule bg-sheet px-4 py-3 text-ink-soft">{children}</p>
}
