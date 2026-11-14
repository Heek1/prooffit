import { useEffect, useState } from 'react'
import type { MatchStatus } from '../api.ts'
import { STATUS_LABEL } from '../report.ts'
import { EvidenceThreads } from './EvidenceThreads.tsx'

// A small, made-up comparison. It runs on the real report pieces (marks, cited lines,
// threads), so what moves here is what the product does, not a picture of it.
const RESUME = [
  'Olena Koval',
  'Full-stack developer',
  'Payments API in ASP.NET Core, PostgreSQL',
  'Admin console in React and TypeScript',
  'Docker images shipped by GitHub Actions',
  'English (B2)',
]

const REQUIREMENTS: { id: string; text: string; status: MatchStatus; lines: number[] }[] = [
  { id: 'D1', text: 'ASP.NET Core and PostgreSQL', status: 'confirmed', lines: [3] },
  { id: 'D2', text: 'React with TypeScript', status: 'confirmed', lines: [4] },
  { id: 'D3', text: 'Docker and Kubernetes', status: 'partial', lines: [5] },
  { id: 'D4', text: 'Kafka', status: 'missing', lines: [] },
]

const STEP_MS = 2600

/** The proof mechanism in miniature: each requirement in turn reaches for its evidence. */
export function ProofDemo() {
  const [index, setIndex] = useState(0)
  // Requirements show their verdict only once the demo has reached them.
  const [reached, setReached] = useState(1)
  // Someone who asked their system for less motion gets a still example, with the option to play it.
  const [playing, setPlaying] = useState(() => !window.matchMedia('(prefers-reduced-motion: reduce)').matches)

  useEffect(() => {
    if (!playing) return
    const timer = window.setInterval(() => {
      setIndex((current) => {
        const next = (current + 1) % REQUIREMENTS.length
        setReached((count) => Math.max(count, next + 1))
        return next
      })
    }, STEP_MS)
    return () => window.clearInterval(timer)
  }, [playing])

  const active = REQUIREMENTS[index]

  return (
    <figure>
      <div
        aria-hidden="true"
        className="relative grid items-start gap-x-10 gap-y-6 sm:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]"
      >
        <ul className="divide-y divide-rule border-y border-rule">
          {REQUIREMENTS.map((requirement, position) => {
            const shown = position < reached
            return (
              <li
                key={requirement.id}
                data-requirement={requirement.id}
                className={`flex items-baseline gap-3 border-l-2 py-3 pr-2 pl-3 ${
                  requirement.id === active.id ? 'border-l-ink bg-sheet/60' : 'border-l-transparent'
                }`}
              >
                <span className={`mark translate-y-0.5 ${shown ? `mark-${requirement.status}` : 'mark-pending'}`} />
                <span className="flex-1 font-medium">{requirement.text}</span>
                <span className="shrink-0 text-sm text-ink-soft">{shown ? STATUS_LABEL[requirement.status] : ''}</span>
              </li>
            )
          })}
        </ul>

        <div className="rounded-sm border border-rule bg-sheet px-4 py-3">
          <ol className="document">
            {RESUME.map((line, position) => {
              const cited = active.lines.includes(position + 1)
              return (
                // Keyed by the active requirement too, so the highlighter sweeps again each time.
                <li
                  key={cited ? `${position}-${active.id}` : position}
                  className={cited ? 'is-cited' : undefined}
                  data-cited={cited ? '' : undefined}
                >
                  {line}
                </li>
              )
            })}
          </ol>
        </div>

        <EvidenceThreads selectedId={active.id} />
      </div>

      <figcaption className="mt-3 flex items-center justify-between gap-4 text-sm text-ink-soft">
        <span>An example: every verdict points at the line that backs it, or says there is none.</span>
        <button
          type="button"
          onClick={() => setPlaying((value) => !value)}
          className="shrink-0 cursor-pointer px-1 py-1 font-medium text-ink underline underline-offset-4 hover:decoration-2"
        >
          {playing ? 'Pause' : 'Play'}
        </button>
      </figcaption>
    </figure>
  )
}
