import type { Page, Request } from '@playwright/test'

/**
 * An in-browser stand-in for the API with a fixed comparison: three requirements, two
 * proven by resume lines and one with no evidence. It records what the page sent, so
 * tests can check requests as well as what is shown.
 */
export type FakeApi = {
  reruns: unknown[]
  deleted: string[]
}

const RESUME_TEXT = [
  'Olena Koval',
  'Full-stack developer',
  'Built a payments API with ASP.NET Core and PostgreSQL',
  'Wrote the React and TypeScript admin console',
].join('\n')

const VACANCY_TEXT = ['Requirements', '- ASP.NET Core', '- React with TypeScript', '- Kubernetes'].join('\n')

function analysis(id: string, edited: boolean, requirements = defaultRequirements()) {
  const now = new Date().toISOString()
  const proven = requirements.filter((r) => r.status === 'confirmed').length
  return {
    id,
    gitHubLogin: null,
    status: 'completed',
    stage: 'done',
    engine: 'keyword',
    error: null,
    createdAt: now,
    completedAt: now,
    expiresAt: new Date(Date.now() + 24 * 3600 * 1000).toISOString(),
    requirementsEdited: edited,
    resume: { id: 'resume-1', fileName: 'sample-resume.pdf', pageCount: 1, text: RESUME_TEXT, uploadedAt: now },
    vacancy: { id: 'vacancy-1', title: 'Full-stack developer', text: VACANCY_TEXT, createdAt: now },
    result: {
      score: Math.round((100 * proven) / requirements.length),
      requirements,
      gitHub: null,
      gitHubNote: null,
      engineNote: null,
      usage: null,
    },
  }
}

function defaultRequirements() {
  return [
    {
      id: 'R1', text: 'ASP.NET Core', importance: 'mustHave', vacancyLine: 2, status: 'confirmed',
      reason: 'Found evidence of ASP.NET Core.', resumeLines: [3], gitHubFacts: [],
    },
    {
      id: 'R2', text: 'React with TypeScript', importance: 'mustHave', vacancyLine: 3, status: 'confirmed',
      reason: 'Found evidence of React and TypeScript.', resumeLines: [4], gitHubFacts: [],
    },
    {
      id: 'R3', text: 'Kubernetes', importance: 'niceToHave', vacancyLine: 4, status: 'missing',
      reason: 'Found no evidence of Kubernetes.', resumeLines: [], gitHubFacts: [],
    },
  ]
}

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
})

export async function fakeApi(page: Page): Promise<FakeApi> {
  const state: FakeApi = { reruns: [], deleted: [] }
  const stored = new Map<string, ReturnType<typeof analysis>>([['analysis-1', analysis('analysis-1', false)]])

  await page.route('**/health/ready', (route) => route.fulfill({ status: 200, body: 'Healthy' }))
  await page.route('**/api/resumes', (route) => route.fulfill(json(stored.get('analysis-1')!.resume, 201)))
  await page.route('**/api/vacancies', (route) => route.fulfill(json(stored.get('analysis-1')!.vacancy, 201)))

  await page.route('**/api/analyses**', async (route) => {
    const request: Request = route.request()
    const path = new URL(request.url()).pathname
    const rerun = path.match(/^\/api\/analyses\/([^/]+)\/rerun$/)
    const one = path.match(/^\/api\/analyses\/([^/]+)$/)

    if (path === '/api/analyses' && request.method() === 'POST') {
      return route.fulfill(json(stored.get('analysis-1'), 202))
    }
    if (rerun && request.method() === 'POST') {
      const body = request.postDataJSON() as { requirements: { text: string; importance: string }[] }
      state.reruns.push(body)
      const requirements = body.requirements.map((r, index) => ({
        id: `R${index + 1}`, text: r.text, importance: r.importance, vacancyLine: null,
        status: 'missing', reason: `Found no evidence of ${r.text}.`, resumeLines: [], gitHubFacts: [],
      }))
      const made = analysis('analysis-2', true, requirements)
      stored.set(made.id, made)
      return route.fulfill(json(made, 202))
    }
    if (one && request.method() === 'GET') {
      const found = stored.get(one[1])
      return found ? route.fulfill(json(found)) : route.fulfill(json({ title: 'Not Found' }, 404))
    }
    if (one && request.method() === 'DELETE') {
      state.deleted.push(one[1])
      stored.delete(one[1])
      return route.fulfill({ status: 204 })
    }
    return route.fulfill(json({ title: 'Unexpected request' }, 500))
  })

  return state
}
