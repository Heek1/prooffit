import type { AnalysisResult, AnalysisStage, MatchStatus, RequirementMatch } from './api.ts'

/** What each verdict is called in the interface. */
export const STATUS_LABEL: Record<MatchStatus, string> = {
  confirmed: 'Proven',
  partial: 'Partly proven',
  missing: 'No evidence',
}

export type StatusCounts = Record<MatchStatus, number>

export function countByStatus(requirements: RequirementMatch[]): StatusCounts {
  const counts: StatusCounts = { confirmed: 0, partial: 0, missing: 0 }
  for (const requirement of requirements) counts[requirement.status]++
  return counts
}

/** "6 of 10 requirements proven", with the singular handled. */
export function headline(requirements: RequirementMatch[]): string {
  const { confirmed } = countByStatus(requirements)
  const noun = requirements.length === 1 ? 'requirement' : 'requirements'
  return `${confirmed} of ${requirements.length} ${noun} proven`
}

/** The verdicts other than "proven", as a sentence. Empty when everything is proven. */
export function remainder(requirements: RequirementMatch[]): string {
  const { partial, missing } = countByStatus(requirements)
  const parts = [
    partial > 0 ? `${partial} partly proven` : '',
    missing > 0 ? `${missing} without evidence` : '',
  ].filter(Boolean)
  return parts.length > 0 ? `${parts.join(', ')}.` : ''
}

/**
 * A must-have counts twice as much as a nice-to-have. Mirrors Scoring.Weight on the
 * server, and is used only to size the segments of the proof bar.
 */
export function weight(requirement: RequirementMatch): number {
  return requirement.importance === 'mustHave' ? 2 : 1
}

/** The requirement to show evidence for first: the first one that has any. */
export function firstWithEvidence(requirements: RequirementMatch[]): RequirementMatch | undefined {
  return requirements.find((r) => r.resumeLines.length > 0 || r.gitHubFacts.length > 0) ?? requirements[0]
}

/** One sentence saying what produced the report and, for a model, what it cost. */
export function engineSentence(engine: string | null, usage: AnalysisResult['usage']): string {
  if (!usage) {
    return engine === 'keyword' || !engine
      ? 'Analysed by keyword matching, without a language model.'
      : `Analysed by ${engine}.`
  }

  const seconds = (usage.durationMs / 1000).toFixed(1)
  const tokens = (usage.inputTokens + usage.outputTokens).toLocaleString('en')
  const calls = usage.calls === 1 ? '1 call' : `${usage.calls} calls`
  return `Analysed by ${usage.model} in ${seconds} s, using ${calls} and ${tokens} tokens.`
}

/** The steps an analysis goes through, in order, as shown while it runs. */
export function progressSteps(usesGitHub: boolean): { stage: AnalysisStage; label: string }[] {
  return [
    { stage: 'readingVacancy' as const, label: 'Reading the vacancy' },
    ...(usesGitHub ? [{ stage: 'collectingGitHub' as const, label: 'Collecting evidence from GitHub' }] : []),
    { stage: 'matching' as const, label: 'Matching requirements to evidence' },
  ]
}

const STAGE_ORDER: AnalysisStage[] = ['queued', 'readingVacancy', 'collectingGitHub', 'matching', 'done']

/** Where a step stands relative to the stage the analysis is on. */
export function stepState(step: AnalysisStage, current: AnalysisStage): 'done' | 'active' | 'waiting' {
  const difference = STAGE_ORDER.indexOf(step) - STAGE_ORDER.indexOf(current)
  return difference < 0 ? 'done' : difference === 0 ? 'active' : 'waiting'
}
