// Empty in development and Docker Compose, where a proxy serves the API on the page's own
// origin. Set VITE_API_URL at build time when the API lives on a different host.
const base: string = import.meta.env.VITE_API_URL ?? ''

export type Resume = {
  id: string
  fileName: string
  pageCount: number
  text: string
  uploadedAt: string
}

export type Vacancy = {
  id: string
  title: string
  text: string
  createdAt: string
}

export type AnalysisStatus = 'queued' | 'running' | 'completed' | 'failed'
export type AnalysisStage = 'queued' | 'readingVacancy' | 'collectingGitHub' | 'matching' | 'done'
export type Importance = 'mustHave' | 'niceToHave'
export type MatchStatus = 'confirmed' | 'partial' | 'missing'

export type RequirementMatch = {
  id: string
  text: string
  importance: Importance
  /** 1-based line of the vacancy the requirement came from. */
  vacancyLine: number | null
  status: MatchStatus
  reason: string
  /** 1-based lines of the resume that support the verdict. */
  resumeLines: number[]
  /** Ids of the GitHub facts that support the verdict. */
  gitHubFacts: string[]
}

export type GitHubFact = {
  id: string
  skill: string
  repositories: { name: string; url: string }[]
}

export type AnalysisResult = {
  score: number
  requirements: RequirementMatch[]
  gitHub: { login: string; repositoriesScanned: number; facts: GitHubFact[] } | null
  gitHubNote: string | null
  engineNote: string | null
  usage: { model: string; calls: number; inputTokens: number; outputTokens: number; durationMs: number } | null
}

export type Analysis = {
  id: string
  gitHubLogin: string | null
  status: AnalysisStatus
  stage: AnalysisStage
  engine: string | null
  error: string | null
  createdAt: string
  completedAt: string | null
  /** When the comparison and its documents are deleted automatically. */
  expiresAt: string
  resume: Resume
  vacancy: Vacancy
  result: AnalysisResult | null
}

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
