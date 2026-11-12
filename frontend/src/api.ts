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

/** A failed request, carrying messages that are ready to show to the user. */
export class ApiError extends Error {
  readonly messages: string[]

  constructor(messages: string[]) {
    super(messages.join(' '))
    this.messages = messages
  }
}

export function uploadResume(file: File): Promise<Resume> {
  const form = new FormData()
  form.append('file', file)
  return send<Resume>('/api/resumes', { method: 'POST', body: form })
}

export function createVacancy(vacancy: { title: string; text: string }): Promise<Vacancy> {
  return send<Vacancy>('/api/vacancies', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(vacancy),
  })
}

/** True when the API is up and can reach its database. */
export async function isServerReady(): Promise<boolean> {
  try {
    return (await fetch(`${base}/health/ready`)).ok
  } catch {
    return false
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
    throw new ApiError(await problemMessages(response))
  }
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
