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

export function listRecent(storage: Storage = localStorage): RecentComparison[] {
  try {
    const parsed: unknown = JSON.parse(storage.getItem(KEY) ?? '[]')
    return Array.isArray(parsed) ? (parsed as RecentComparison[]) : []
  } catch {
    return []
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
