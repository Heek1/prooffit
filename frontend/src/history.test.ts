import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { forget, listRecent, remember, type RecentComparison } from './history.ts'

/** The part of the Storage interface the history uses, backed by a map. */
function memoryStorage(initial: Record<string, string> = {}): Storage {
  const items = new Map(Object.entries(initial))
  return {
    getItem: (key: string) => items.get(key) ?? null,
    setItem: (key: string, value: string) => void items.set(key, value),
  } as Storage
}

// The fixtures are dated 12 November 2026, so the clock is held an hour after them.
beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(new Date('2026-11-12T09:00:00Z'))
})

afterEach(() => {
  vi.useRealTimers()
})

function comparison(id: string, score: number | null = null): RecentComparison {
  return { id, title: `Vacancy ${id}`, createdAt: '2026-11-12T08:00:00Z', score }
}

describe('recent comparisons', () => {
  it('lists the newest first', () => {
    const storage = memoryStorage()

    remember(comparison('a'), storage)
    remember(comparison('b'), storage)

    expect(listRecent(storage).map((entry) => entry.id)).toEqual(['b', 'a'])
  })

  it('updates a comparison that is already listed instead of repeating it', () => {
    const storage = memoryStorage()
    remember(comparison('a'), storage)
    remember(comparison('b'), storage)

    remember(comparison('a', 76), storage)

    expect(listRecent(storage)).toEqual([comparison('a', 76), comparison('b')])
  })

  it('keeps only the ten most recent', () => {
    const storage = memoryStorage()

    for (let index = 1; index <= 12; index++) remember(comparison(String(index)), storage)

    const ids = listRecent(storage).map((entry) => entry.id)
    expect(ids).toHaveLength(10)
    expect(ids[0]).toBe('12')
    expect(ids).not.toContain('1')
  })

  it('is empty when storage holds something that is not a list', () => {
    expect(listRecent(memoryStorage({ 'prooffit.recent': 'not json' }))).toEqual([])
    expect(listRecent(memoryStorage({ 'prooffit.recent': '{"a":1}' }))).toEqual([])
  })

  it('carries on when storage refuses to save', () => {
    const full = {
      getItem: () => null,
      setItem: () => {
        throw new Error('QuotaExceededError')
      },
    } as unknown as Storage

    expect(() => remember(comparison('a'), full)).not.toThrow()
  })
})

describe('comparisons the server no longer has', () => {
  it('leaves out entries older than a day, which the server has deleted', () => {
    const storage = memoryStorage()
    remember(comparison('old'), storage)
    remember({ ...comparison('new'), createdAt: '2026-11-13T07:00:00Z' }, storage)

    const now = Date.parse('2026-11-13T09:00:00Z')

    expect(listRecent(storage, now).map((entry) => entry.id)).toEqual(['new'])
  })

  it('forgets a comparison that was deleted', () => {
    const storage = memoryStorage()
    remember(comparison('a'), storage)
    remember(comparison('b'), storage)

    forget('a', storage)

    expect(listRecent(storage, Date.parse('2026-11-12T09:00:00Z')).map((entry) => entry.id)).toEqual(['b'])
  })
})
