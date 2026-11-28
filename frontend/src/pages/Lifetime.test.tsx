// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { deleteAnalysis } from '../api.ts'
import { listRecent, remember } from '../history.ts'
import '../test/dom.ts'
import { Lifetime } from './ReportPage.tsx'

vi.mock('../api.ts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api.ts')>()),
  deleteAnalysis: vi.fn(),
}))

const remove = vi.mocked(deleteAnalysis)

function renderLifetime() {
  render(
    <MemoryRouter initialEntries={['/analyses/a1']}>
      <Routes>
        <Route path="/analyses/a1" element={<Lifetime analysis={{ id: 'a1', expiresAt: '2099-01-01T12:00:00Z' }} />} />
        <Route path="/" element={<p>Start page</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('deleting a report', () => {
  beforeEach(() => {
    remove.mockReset()
    remember({ id: 'a1', title: 'Backend developer', createdAt: new Date().toISOString(), score: 60 })
  })

  it('asks first, and keeps the report when the user changes their mind', async () => {
    renderLifetime()

    await userEvent.click(screen.getByRole('button', { name: 'Delete now' }))
    expect(screen.getByText(/This cannot be undone/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Keep' }))
    expect(screen.getByRole('button', { name: 'Delete now' })).toBeInTheDocument()
    expect(remove).not.toHaveBeenCalled()
  })

  it('deletes the report, forgets it in this browser and goes to the start page', async () => {
    remove.mockResolvedValue()
    renderLifetime()

    await userEvent.click(screen.getByRole('button', { name: 'Delete now' }))
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

    expect(remove).toHaveBeenCalledWith('a1')
    expect(await screen.findByText('Start page')).toBeInTheDocument()
    expect(listRecent()).toEqual([])
  })

  it('says so when the report could not be deleted, and keeps it listed', async () => {
    remove.mockRejectedValue(new Error('offline'))
    renderLifetime()

    await userEvent.click(screen.getByRole('button', { name: 'Delete now' }))
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The report could not be deleted. Try again.')
    expect(screen.getByRole('button', { name: 'Delete now' })).toBeInTheDocument()
    expect(listRecent()).toHaveLength(1)
  })
})
