import { describe, expect, it } from 'vitest'
import type { MatchStatus, RequirementMatch } from './api.ts'
import {
  countByStatus,
  engineSentence,
  firstWithEvidence,
  headline,
  progressSteps,
  remainder,
  stepState,
  weight,
} from './report.ts'

function requirement(status: MatchStatus, overrides: Partial<RequirementMatch> = {}): RequirementMatch {
  return {
    id: 'R1',
    text: 'Docker',
    importance: 'mustHave',
    vacancyLine: 1,
    status,
    reason: 'A reason.',
    resumeLines: [],
    gitHubFacts: [],
    ...overrides,
  }
}

describe('the summary of a report', () => {
  const mixed = [requirement('confirmed'), requirement('confirmed'), requirement('partial'), requirement('missing')]

  it('counts requirements by verdict', () => {
    expect(countByStatus(mixed)).toEqual({ confirmed: 2, partial: 1, missing: 1 })
  })

  it('says how many requirements are proven', () => {
    expect(headline(mixed)).toBe('2 of 4 requirements proven')
    expect(headline([requirement('missing')])).toBe('0 of 1 requirement proven')
  })

  it('describes the rest, and says nothing when everything is proven', () => {
    expect(remainder(mixed)).toBe('1 partly proven, 1 without evidence.')
    expect(remainder([requirement('confirmed'), requirement('missing')])).toBe('1 without evidence.')
    expect(remainder([requirement('confirmed')])).toBe('')
  })
})

describe('the proof bar', () => {
  it('gives a must-have twice the width of a nice-to-have', () => {
    expect(weight(requirement('confirmed'))).toBe(2)
    expect(weight(requirement('confirmed', { importance: 'niceToHave' }))).toBe(1)
  })
})

describe('the requirement selected when a report opens', () => {
  it('is the first one with evidence to show', () => {
    const withLine = requirement('confirmed', { id: 'R2', resumeLines: [4] })
    const withFact = requirement('confirmed', { id: 'R3', gitHubFacts: ['G1'] })

    expect(firstWithEvidence([requirement('missing'), withLine, withFact])?.id).toBe('R2')
    expect(firstWithEvidence([requirement('missing'), withFact])?.id).toBe('R3')
  })

  it('is the first requirement when none has evidence, and nothing for an empty report', () => {
    expect(firstWithEvidence([requirement('missing', { id: 'R7' })])?.id).toBe('R7')
    expect(firstWithEvidence([])).toBeUndefined()
  })
})

describe('the line saying what produced a report', () => {
  it('names keyword matching when no model was used', () => {
    expect(engineSentence('keyword', null)).toBe('Analysed by keyword matching, without a language model.')
  })

  it('gives the model, the time and the cost when one was used', () => {
    const usage = { model: 'gemini-flash-latest', calls: 2, inputTokens: 3000, outputTokens: 412, durationMs: 4130 }

    expect(engineSentence('gemini-flash-latest', usage)).toBe(
      'Analysed by gemini-flash-latest in 4.1 s, using 2 calls and 3,412 tokens.',
    )
  })
})

describe('the progress of a running analysis', () => {
  it('includes the GitHub step only when a username was given', () => {
    expect(progressSteps(false).map((step) => step.stage)).toEqual(['readingVacancy', 'matching'])
    expect(progressSteps(true).map((step) => step.stage)).toEqual(['readingVacancy', 'collectingGitHub', 'matching'])
  })

  it('marks steps before the current stage as done and later ones as waiting', () => {
    expect(stepState('readingVacancy', 'matching')).toBe('done')
    expect(stepState('matching', 'matching')).toBe('active')
    expect(stepState('matching', 'readingVacancy')).toBe('waiting')
    expect(stepState('readingVacancy', 'queued')).toBe('waiting')
  })
})
