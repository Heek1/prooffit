// @vitest-environment jsdom
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { GitHubFact } from '../api.ts'
import { requirement } from '../test/dom.ts'
import { FactSentence, RequirementList } from './RequirementList.tsx'

const resumeLines = ['Backend developer', 'Containerised services with Docker', 'PostgreSQL in production']

const dockerFact: GitHubFact = {
  id: 'G1',
  skill: 'Docker',
  repositories: [
    { name: 'shop-api', url: 'https://github.com/a/shop-api', file: 'Dockerfile', fileUrl: 'https://github.com/a/shop-api/blob/main/Dockerfile', pushedAt: null },
  ],
}

describe('the list of requirements', () => {
  const requirements = [
    requirement({ id: 'R1', text: 'Docker', status: 'confirmed', resumeLines: [2], gitHubFacts: ['G1'] }),
    requirement({ id: 'R2', text: 'Kubernetes', status: 'missing', reason: 'Not mentioned.' }),
    requirement({ id: 'R3', text: 'PostgreSQL', importance: 'niceToHave', status: 'partial', resumeLines: [3] }),
  ]

  it('groups requirements by importance and shows each verdict', () => {
    render(<RequirementList requirements={requirements} resumeLines={resumeLines} facts={[dockerFact]} selectedId={null} onSelect={() => {}} />)

    const mustHave = screen.getByRole('heading', { name: 'Must have' }).closest('section')!
    const niceToHave = screen.getByRole('heading', { name: 'Nice to have' }).closest('section')!
    expect(within(mustHave).getByRole('button', { name: /Docker\s*Proven/ })).toBeInTheDocument()
    expect(within(mustHave).getByRole('button', { name: /Kubernetes\s*No evidence/ })).toBeInTheDocument()
    expect(within(niceToHave).getByRole('button', { name: /PostgreSQL\s*Partly proven/ })).toBeInTheDocument()
  })

  it('quotes the resume lines and GitHub facts it cites', () => {
    render(<RequirementList requirements={requirements} resumeLines={resumeLines} facts={[dockerFact]} selectedId={null} onSelect={() => {}} />)

    const docker = screen.getByRole('button', { name: /Docker/ }).closest('li')!
    expect(within(docker).getByText('Containerised services with Docker')).toBeInTheDocument()
    // The line number reads "Resume line 2" to a screen reader and shows as "2".
    expect(within(docker).getByText('Resume line', { exact: false }).parentElement).toHaveTextContent('Resume line 2')
    expect(within(docker).getByRole('link', { name: 'shop-api' })).toHaveAttribute('href', 'https://github.com/a/shop-api')

    const kubernetes = screen.getByRole('button', { name: /Kubernetes/ }).closest('li')!
    expect(within(kubernetes).getByText('Not mentioned.')).toBeInTheDocument()
    expect(within(kubernetes).queryByRole('link')).not.toBeInTheDocument()
  })

  it('reports the requirement chosen and marks the selected one as pressed', async () => {
    const onSelect = vi.fn()
    render(<RequirementList requirements={requirements} resumeLines={resumeLines} facts={[]} selectedId="R3" onSelect={onSelect} />)

    await userEvent.click(screen.getByRole('button', { name: /Kubernetes/ }))

    expect(onSelect).toHaveBeenCalledWith('R2')
    expect(screen.getByRole('button', { name: /PostgreSQL/ })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: /Docker/ })).toHaveAttribute('aria-pressed', 'false')
  })
})

describe('a sentence about a GitHub fact', () => {
  it('links the file the rule matched', () => {
    render(<FactSentence fact={dockerFact} />)

    expect(screen.getByRole('link', { name: 'Dockerfile' })).toHaveAttribute('href', 'https://github.com/a/shop-api/blob/main/Dockerfile')
  })

  it('names three repositories and counts the rest', () => {
    const names = ['one', 'two', 'three', 'four', 'five']
    const fact: GitHubFact = {
      id: 'G2',
      skill: 'TypeScript',
      repositories: names.map((name) => ({ name, url: `https://github.com/a/${name}`, file: null, fileUrl: null, pushedAt: null })),
    }

    const { container } = render(<FactSentence fact={fact} />)

    expect(screen.getAllByRole('link').map((link) => link.textContent)).toEqual(['one', 'two', 'three'])
    expect(container).toHaveTextContent('On GitHub: TypeScript in one, two, three and 2 more')
  })
})
