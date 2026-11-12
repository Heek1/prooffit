import type { RequirementMatch } from '../api.ts'
import { countByStatus, STATUS_LABEL, weight } from '../report.ts'

/**
 * The whole comparison at a glance: one segment per requirement, twice as wide for a
 * must-have, filled when proven, half-filled when partly proven, empty when not.
 */
export function ProofBar({ requirements }: { requirements: RequirementMatch[] }) {
  const counts = countByStatus(requirements)
  const summary = `${counts.confirmed} proven, ${counts.partial} partly proven, ${counts.missing} without evidence`

  return (
    <div>
      <div role="img" aria-label={summary} className="flex h-4 gap-0.5">
        {requirements.map((requirement) => (
          <span
            key={requirement.id}
            title={`${requirement.text}: ${STATUS_LABEL[requirement.status]}`}
            style={{ flexGrow: weight(requirement) }}
            className={`mark mark-${requirement.status} h-auto w-auto basis-0`}
          />
        ))}
      </div>

      <ul className="mt-3 flex flex-wrap gap-x-6 gap-y-1 text-sm text-ink-soft">
        {(['confirmed', 'partial', 'missing'] as const).map((status) => (
          <li key={status} className="flex items-center gap-2">
            <span className={`mark mark-${status}`} aria-hidden="true" />
            {STATUS_LABEL[status]}
          </li>
        ))}
      </ul>
    </div>
  )
}
