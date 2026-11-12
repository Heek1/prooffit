import type { GitHubFact, Importance, RequirementMatch } from '../api.ts'
import { STATUS_LABEL } from '../report.ts'

type RequirementListProps = {
  requirements: RequirementMatch[]
  resumeLines: string[]
  facts: GitHubFact[]
  selectedId: string | null
  onSelect: (id: string) => void
}

const SECTIONS: { importance: Importance; title: string }[] = [
  { importance: 'mustHave', title: 'Must have' },
  { importance: 'niceToHave', title: 'Nice to have' },
]

/** Every requirement with its verdict, the reason, and the evidence quoted in place. */
export function RequirementList({ requirements, resumeLines, facts, selectedId, onSelect }: RequirementListProps) {
  return (
    <div className="space-y-8">
      {SECTIONS.map(({ importance, title }) => {
        const group = requirements.filter((requirement) => requirement.importance === importance)
        if (group.length === 0) return null

        return (
          <section key={importance}>
            <h2 className="font-semibold">{title}</h2>
            <ul className="mt-2 divide-y divide-rule border-y border-rule">
              {group.map((requirement) => (
                <RequirementRow
                  key={requirement.id}
                  requirement={requirement}
                  resumeLines={resumeLines}
                  facts={facts}
                  selected={requirement.id === selectedId}
                  onSelect={() => onSelect(requirement.id)}
                />
              ))}
            </ul>
          </section>
        )
      })}
    </div>
  )
}

type RequirementRowProps = {
  requirement: RequirementMatch
  resumeLines: string[]
  facts: GitHubFact[]
  selected: boolean
  onSelect: () => void
}

function RequirementRow({ requirement, resumeLines, facts, selected, onSelect }: RequirementRowProps) {
  const cited = facts.filter((fact) => requirement.gitHubFacts.includes(fact.id))
  const hasEvidence = requirement.resumeLines.length > 0 || cited.length > 0

  return (
    <li className={`border-l-2 py-4 pr-2 pl-3 ${selected ? 'border-l-ink bg-desk/50' : 'border-l-transparent'}`}>
      {/* Choosing a requirement marks its evidence in the resume and GitHub panels. */}
      <button
        type="button"
        onClick={onSelect}
        aria-pressed={selected}
        className="flex w-full cursor-pointer items-baseline gap-3 text-left"
      >
        <span className={`mark mark-${requirement.status} translate-y-0.5`} aria-hidden="true" />
        <span className="flex-1 font-medium">{requirement.text}</span>
        <span className="shrink-0 text-sm text-ink-soft">{STATUS_LABEL[requirement.status]}</span>
      </button>

      <div className="mt-1 pl-[1.625rem]">
        <p className="text-ink-soft">{requirement.reason}</p>

        {hasEvidence && (
          <ul className="mt-3 space-y-1.5 border-l-2 border-marker-deep pl-3">
            {requirement.resumeLines.map((line) => (
              <li key={line} className="grid grid-cols-[1.75rem_minmax(0,1fr)] items-baseline gap-2">
                <span className="text-right text-xs text-ink-soft tabular-nums">
                  <span className="sr-only">Resume line </span>
                  {line}
                </span>
                <span className="font-serif text-[0.95rem] leading-relaxed">{resumeLines[line - 1]}</span>
              </li>
            ))}
            {cited.map((fact) => (
              <li key={fact.id} className="text-sm">
                <FactSentence fact={fact} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </li>
  )
}

const MAX_REPOSITORIES_SHOWN = 3

/** "On GitHub: Docker in shop-api, infra and 2 more", with each repository linked. */
export function FactSentence({ fact }: { fact: GitHubFact }) {
  const shown = fact.repositories.slice(0, MAX_REPOSITORIES_SHOWN)
  const hidden = fact.repositories.length - shown.length

  return (
    <>
      On GitHub: {fact.skill} in{' '}
      {shown.map((repository, index) => (
        <span key={repository.url}>
          {index > 0 && ', '}
          <a href={repository.url} target="_blank" rel="noreferrer" className="underline underline-offset-2 hover:decoration-2">
            {repository.name}
          </a>
        </span>
      ))}
      {hidden > 0 && ` and ${hidden} more`}
    </>
  )
}
