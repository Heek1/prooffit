// Shared set-up for component tests, which start with `// @vitest-environment jsdom`.
// The rest of the suite tests plain functions and runs without a DOM.
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'
import type { RequirementMatch } from '../api.ts'

afterEach(() => {
  cleanup()
  localStorage.clear()
})

export function requirement(overrides: Partial<RequirementMatch> = {}): RequirementMatch {
  return {
    id: 'R1',
    text: 'Docker',
    importance: 'mustHave',
    vacancyLine: 1,
    status: 'confirmed',
    reason: 'A reason.',
    resumeLines: [],
    gitHubFacts: [],
    ...overrides,
  }
}
