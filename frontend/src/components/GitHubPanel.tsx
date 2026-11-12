import { useEffect, useRef } from 'react'
import type { AnalysisResult } from '../api.ts'
import { Sheet } from './Sheet.tsx'

type GitHubPanelProps = {
  evidence: NonNullable<AnalysisResult['gitHub']>
  /** Ids of the facts the selected requirement cites. The first one is scrolled into view. */
  cited: string[]
}

/** Everything the candidate's public repositories showed, with the cited facts marked. */
export function GitHubPanel({ evidence, cited }: GitHubPanelProps) {
  const list = useRef<HTMLUListElement>(null)
  const firstCited = evidence.facts.findIndex((fact) => cited.includes(fact.id))

  useEffect(() => {
    const row = list.current?.children[firstCited] as HTMLElement | undefined
    if (!list.current || !row) return

    // Scrolls the list itself, never the page.
    list.current.scrollTo({ top: row.offsetTop - list.current.clientHeight / 3, behavior: 'smooth' })
  }, [firstCited])

  const repositories = evidence.repositoriesScanned === 1 ? '1 repository' : `${evidence.repositoriesScanned} repositories`

  return (
    <Sheet title="GitHub" detail={`${evidence.login}, ${repositories} scanned`}>
      {evidence.facts.length === 0 ? (
        <p className="text-ink-soft">No known technologies were found in the public repositories.</p>
      ) : (
        <ul
          ref={list}
          tabIndex={0}
          aria-label="Skills found on GitHub"
          className="relative max-h-56 space-y-0.5 overflow-y-auto text-sm"
        >
          {evidence.facts.map((fact) => (
            <li
              key={fact.id}
              className={`flex items-baseline justify-between gap-4 px-2 py-1 ${cited.includes(fact.id) ? 'bg-marker' : ''}`}
            >
              <span className="font-medium">{fact.skill}</span>
              <span className="text-right text-ink-soft">
                {fact.repositories.length === 1 ? fact.repositories[0].name : `${fact.repositories.length} repositories`}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Sheet>
  )
}
