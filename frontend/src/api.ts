import type { components } from './api.generated.ts'

// Empty in development and Docker Compose, where a proxy serves the API on the page's own
// origin. Set VITE_API_URL at build time when the API lives on a different host.
const base: string = import.meta.env.VITE_API_URL ?? ''

// The shapes the API sends, generated from its OpenAPI document (npm run api:types), so a
// change on the server that the page has not caught up with fails the type check.
type Schemas = components['schemas']

/** `ignoredCharacters` counts characters in the PDF left out because a reader could not see them. */
export type Resume = Schemas['ResumeResponse']
export type Vacancy = Schemas['VacancyResponse']
export type AnalysisStatus = Schemas['AnalysisStatus']
export type AnalysisStage = Schemas['AnalysisStage']
export type Importance = Schemas['Importance']
export type MatchStatus = Schemas['MatchStatus']
/** Line numbers, in the vacancy and the resume, start at 1. */
export type RequirementMatch = Schemas['RequirementMatch']
export type GitHubFact = Schemas['GitHubFact']
/** File and date are missing from profiles scanned before they were recorded. */
export type GitHubRepository = Schemas['RepositoryRef']
export type AnalysisResult = Schemas['AnalysisResult']
export type Analysis = Schemas['AnalysisResponse']

export type AnalysisProgress = Pick<Analysis, 'status' | 'stage'>

/** A failed request, carrying messages that are ready to show to the user. */
export class ApiError extends Error {
  readonly messages: string[]
  readonly status: number | null

  constructor(messages: string[], status: number | null = null) {
    super(messages.join(' '))
    this.messages = messages
    this.status = status
  }
}

export function uploadResume(file: File): Promise<Resume> {
  const form = new FormData()
  form.append('file', file)
  return send<Resume>('/api/resumes', { method: 'POST', body: form })
}

export function createVacancy(vacancy: { title: string; text: string }): Promise<Vacancy> {
  return send<Vacancy>('/api/vacancies', json(vacancy))
}

export function createAnalysis(request: {
  resumeId: string
  vacancyId: string
  gitHubLogin: string | null
}): Promise<Analysis> {
  return send<Analysis>('/api/analyses', json(request))
}

export function getAnalysis(id: string): Promise<Analysis> {
  return send<Analysis>(`/api/analyses/${id}`, {})
}

/** Compares the same documents again against requirements the user has corrected. */
export function rerunAnalysis(
  id: string,
  requirements: { text: string; importance: Importance; vacancyLine: number | null }[],
): Promise<Analysis> {
  return send<Analysis>(`/api/analyses/${id}/rerun`, json({ requirements }))
}

/** Deletes a comparison and its documents at once. */
export function deleteAnalysis(id: string): Promise<void> {
  return send<void>(`/api/analyses/${id}`, { method: 'DELETE' })
}

/**
 * Follows an analysis as it runs. The server closes the stream when the analysis ends, and
 * the browser would then reconnect, so the stream is closed here on the final event.
 * Returns a function that stops listening.
 */
export function watchAnalysis(
  id: string,
  onProgress: (progress: AnalysisProgress) => void,
  onError: () => void,
): () => void {
  const events = new EventSource(`${base}/api/analyses/${id}/events`)

  events.addEventListener('progress', (event) => {
    const progress = JSON.parse((event as MessageEvent<string>).data) as AnalysisProgress
    if (progress.status === 'completed' || progress.status === 'failed') events.close()
    onProgress(progress)
  })
  events.onerror = () => {
    events.close()
    onError()
  }

  return () => events.close()
}

/** True when the API is up and can reach its database. */
export async function isServerReady(): Promise<boolean> {
  try {
    return (await fetch(`${base}/health/ready`)).ok
  } catch {
    return false
  }
}

function json(body: unknown): RequestInit {
  return {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  }
}

async function send<T>(path: string, init: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(base + path, init)
  } catch {
    throw new ApiError(["Can't reach the server. Check that the API is running, then try again."])
  }

  if (!response.ok) {
    throw new ApiError(await problemMessages(response), response.status)
  }
  // A successful delete has no body to read.
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

// The API reports errors as RFC 9457 problem details. Validation failures add an `errors`
// map of field name to messages; everything else has only a title.
async function problemMessages(response: Response): Promise<string[]> {
  const problem = (await response.json().catch(() => null)) as {
    title?: string
    errors?: Record<string, string[]>
  } | null

  const fieldMessages = Object.values(problem?.errors ?? {}).flat()
  if (fieldMessages.length > 0) return fieldMessages
  return [problem?.title ?? `The server answered with status ${response.status}.`]
}
