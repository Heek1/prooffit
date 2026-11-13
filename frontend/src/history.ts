/**
 * The comparisons made in this browser. There are no accounts yet, so the list lives in
 * local storage: it is a convenience for finding a report again, and nothing breaks when
 * storage is empty, full or blocked.
 */
export type RecentComparison = {
  id: string
  title: string
  createdAt: string
  /** Null until the analysis has completed. */
  score: number | null
}

const KEY = 'prooffit.recent'
const MAX_ENTRIES = 10

// The server deletes a comparison a day after it is made, so older entries lead nowhere.
const LIFETIME_MS = 24 * 60 * 60 * 1000

export function listRecent(storage: Storage = localStorage, now: number = Date.now()): RecentComparison[] {
  try {
    const parsed: unknown = JSON.parse(storage.getItem(KEY) ?? '[]')
    if (!Array.isArray(parsed)) return []
    return (parsed as RecentComparison[]).filter((entry) => now - Date.parse(entry.createdAt) < LIFETIME_MS)
  } catch {
    return []
  }
}

/** Drops a comparison from the list, as when it has been deleted. */
export function forget(id: string, storage: Storage = localStorage): void {
  try {
    storage.setItem(KEY, JSON.stringify(listRecent(storage).filter((entry) => entry.id !== id)))
  } catch {
    // Nothing to do: a list that cannot be saved is rebuilt from what the server still has.
  }
}

/** Adds a comparison, or updates it when it is already listed. Newest first. */
export function remember(comparison: RecentComparison, storage: Storage = localStorage): void {
  const others = listRecent(storage).filter((entry) => entry.id !== comparison.id)
  try {
    storage.setItem(KEY, JSON.stringify([comparison, ...others].slice(0, MAX_ENTRIES)))
  } catch {
    // Storage can be full or disabled. The comparison still exists at its own address.
  }
}
