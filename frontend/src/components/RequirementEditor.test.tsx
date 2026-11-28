// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, rerunAnalysis, type Analysis } from '../api.ts'
import { listRecent } from '../history.ts'
import { requirement } from '../test/dom.ts'
import { RequirementEditor } from './RequirementEditor.tsx'

vi.mock('../api.ts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api.ts')>()),
  rerunAnalysis: vi.fn(),
}))

const rerun = vi.mocked(rerunAnalysis)

const requirements = [
  requirement({ id: 'R1', text: 'Docker', vacancyLine: 3 }),
  requirement({ id: 'R2', text: 'Kubernetes', vacancyLine: 4 }),
]

function renderEditor(onCancel = () => {}) {
  render(
    <MemoryRouter initialEntries={['/analyses/a1']}>
      <Routes>
        <Route
          path="/analyses/a1"
          element={<RequirementEditor analysisId="a1" vacancyTitle="Backend developer" requirements={requirements} onCancel={onCancel} />}
        />
        <Route path="/analyses/:id" element={<p>New report page</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('editing the requirements', () => {
  // In braces: a function returned from beforeEach is run as clean-up, and mockReset returns the mock.
  beforeEach(() => {
    rerun.mockReset()
  })

  it('compares again with the corrected list and opens the new report', async () => {
    rerun.mockResolvedValue({ id: 'a2', createdAt: '2026-11-28T10:00:00Z' } as Analysis)
    renderEditor()

    await userEvent.click(screen.getByRole('button', { name: 'Remove requirement 2' }))
    await userEvent.click(screen.getByRole('button', { name: 'Add a requirement' }))
    await userEvent.type(screen.getByRole('textbox', { name: 'Requirement 2' }), '  Terraform  ')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Importance of requirement 2' }), 'Nice to have')
    await userEvent.click(screen.getByRole('button', { name: 'Compare again' }))

    expect(rerun).toHaveBeenCalledWith('a1', [
      { text: 'Docker', importance: 'mustHave', vacancyLine: 3 },
      { text: 'Terraform', importance: 'niceToHave', vacancyLine: null },
    ])
    expect(await screen.findByText('New report page')).toBeInTheDocument()
    expect(listRecent()).toEqual([{ id: 'a2', title: 'Backend developer (edited)', createdAt: '2026-11-28T10:00:00Z', score: null }])
  })

  it('leaves out blank requirements and refuses an empty list', async () => {
    renderEditor()

    await userEvent.clear(screen.getByRole('textbox', { name: 'Requirement 1' }))
    await userEvent.click(screen.getByRole('button', { name: 'Remove requirement 2' }))
    await userEvent.click(screen.getByRole('button', { name: 'Compare again' }))

    expect(screen.getByText('Keep at least one requirement.')).toBeInTheDocument()
    expect(rerun).not.toHaveBeenCalled()
  })

  it("shows the server's messages when it rejects the list", async () => {
    rerun.mockRejectedValue(new ApiError(['A requirement can have at most 300 characters.'], 400))
    renderEditor()

    await userEvent.click(screen.getByRole('button', { name: 'Compare again' }))

    expect(await screen.findByText('A requirement can have at most 300 characters.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Compare again' })).toBeEnabled()
  })

  it('can be cancelled', async () => {
    const onCancel = vi.fn()
    renderEditor(onCancel)

    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onCancel).toHaveBeenCalled()
    expect(rerun).not.toHaveBeenCalled()
  })
})
