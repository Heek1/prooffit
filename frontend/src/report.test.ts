import { describe, expect, it } from 'vitest'
import type { MatchStatus, RequirementMatch } from './api.ts'
import {
  countByStatus,
  engineSentence,
  firstWithEvidence,
  headline,
  lastPushed,
  reportSummary,
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

describe('lastPushed', () => {
  it('names the month of the most recent push', () => {
    expect(
      lastPushed([
        { name: 'old', url: 'u1', pushedAt: '2025-01-02T00:00:00Z' },
        { name: 'new', url: 'u2', pushedAt: '2026-03-14T09:00:00Z' },
      ]),
    ).toBe('March 2026')
  })

  it('says nothing for repositories recorded without a date', () => {
    expect(lastPushed([{ name: 'infra', url: 'u' }])).toBeNull()
  })
})

describe('reportSummary', () => {
  const analysis: Parameters<typeof reportSummary>[0] = {
    id: 'a1', gitHubLogin: 'olena-dev', status: 'completed', stage: 'done', engine: 'keyword', error: null,
    createdAt: '2026-11-20T10:00:00Z', completedAt: '2026-11-20T10:00:05Z', expiresAt: '2026-11-21T10:00:00Z',
    requirementsEdited: false,
    resume: { id: 'r1', fileName: 'cv.pdf', pageCount: 1, uploadedAt: '', ignoredCharacters: 0,
      text: 'Olena Koval\nBuilt a payments API with ASP.NET Core' },
    vacancy: { id: 'v1', title: 'Backend developer', text: '', createdAt: '' },
    result: {
      score: 67, engineNote: null, gitHubNote: null, usage: null,
      gitHub: { login: 'olena-dev', repositoriesScanned: 2, facts: [
        { id: 'G1', skill: 'Kubernetes', repositories: [{ name: 'infra', url: 'u', file: 'k8s/deployment.yaml' }] },
      ] },
      requirements: [
        requirement('confirmed', { id: 'R1', text: 'ASP.NET Core', reason: 'Found evidence of ASP.NET Core.', resumeLines: [2] }),
        requirement('confirmed', { id: 'R2', text: 'Kubernetes', importance: 'niceToHave', reason: 'Manifests on GitHub.', gitHubFacts: ['G1'] }),
        requirement('missing', { id: 'R3', text: 'Kafka', reason: 'Found no evidence of Kafka.' }),
      ],
    },
  }

  it('lists each requirement under its verdict with the evidence quoted', () => {
    expect(reportSummary(analysis, 'https://prooffit.onrender.com/analyses/a1')).toBe([
      'ProofFit report: Backend developer',
      '2 of 3 requirements proven. Score 67 out of 100, with must-haves counting double.',
      '',
      'Proven',
      '- ASP.NET Core (must-have): Found evidence of ASP.NET Core.',
      '  Resume line 2: "Built a payments API with ASP.NET Core"',
      '- Kubernetes (nice to have): Manifests on GitHub.',
      '  GitHub: Kubernetes in infra (k8s/deployment.yaml)',
      '',
      'No evidence',
      '- Kafka (must-have): Found no evidence of Kafka.',
      '',
      'Full report: https://prooffit.onrender.com/analyses/a1 (available until Nov 21, 2026)',
    ].join('\n'))
  })
})
