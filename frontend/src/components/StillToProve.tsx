import type { RequirementMatch } from '../api.ts'

type StillToProveProps = {
  requirements: RequirementMatch[]
  onSelect: (id: string) => void
}

/** What the resume has not proven yet, gathered in one place so the next step is plain. */
export function StillToProve({ requirements, onSelect }: StillToProveProps) {
  const open = requirements.filter((requirement) => requirement.status !== 'confirmed')
  if (open.length === 0) return null

  return (
    <section aria-labelledby="still-to-prove" className="border-l-4 border-marker-deep bg-sheet px-5 py-4">
      <h2 id="still-to-prove" className="font-semibold">
        Still to prove
      </h2>
      <ul className="mt-1 flex flex-wrap gap-x-6">
        {open.map((requirement) => (
          <li key={requirement.id}>
            <button
              type="button"
              onClick={() => onSelect(requirement.id)}
              className="flex min-h-9 cursor-pointer items-center gap-2 text-left hover:underline hover:underline-offset-4"
            >
              <span className={`mark mark-${requirement.status}`} aria-hidden="true" />
              <span className="font-medium">{requirement.text}</span>
              <span className="shrink-0 text-sm text-ink-soft">
                {requirement.importance === 'mustHave' ? 'must have' : 'nice to have'}
              </span>
            </button>
          </li>
        ))}
      </ul>
      <p className="mt-2 text-sm text-ink-soft">
        Add a resume line or a public repository that shows these, then compare again.
      </p>
    </section>
  )
}
