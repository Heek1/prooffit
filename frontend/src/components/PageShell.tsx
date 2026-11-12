import type { ReactNode } from 'react'
import { Link } from 'react-router'

/** The frame every page shares: the product name linking home, and an optional action beside it. */
export function PageShell({ action, children }: { action?: ReactNode; children: ReactNode }) {
  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-8 sm:py-12">
      <nav className="flex min-h-10 items-center justify-between gap-4">
        <Link to="/" className="text-lg font-semibold">
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
