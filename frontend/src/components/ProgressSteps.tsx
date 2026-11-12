import type { AnalysisStage } from '../api.ts'
import { progressSteps, stepState } from '../report.ts'

const MARK = { done: 'mark-confirmed', active: 'mark-partial mark-working', waiting: 'mark-missing' }

/** The steps of a running analysis, with the same marks the report uses for verdicts. */
export function ProgressSteps({ stage, usesGitHub }: { stage: AnalysisStage; usesGitHub: boolean }) {
  return (
    <ol className="space-y-3">
      {progressSteps(usesGitHub).map((step) => {
        const state = stepState(step.stage, stage)
        return (
          <li
            key={step.stage}
            aria-current={state === 'active' ? 'step' : undefined}
            className={`flex items-center gap-3 ${state === 'waiting' ? 'text-ink-soft' : ''}`}
          >
            <span className={`mark ${MARK[state]}`} aria-hidden="true" />
            {step.label}
            {state === 'done' && <span className="sr-only">(done)</span>}
          </li>
        )
      })}
    </ol>
  )
}
