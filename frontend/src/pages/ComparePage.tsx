import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { ApiError, createAnalysis, createVacancy, isServerReady, uploadResume, type Resume } from '../api.ts'
import { PageShell } from '../components/PageShell.tsx'
import { RecentComparisons } from '../components/RecentComparisons.tsx'
import { ResumeSheet } from '../components/ResumeSheet.tsx'
import { ErrorList, TextButton } from '../components/Sheet.tsx'
import { VacancySheet, type VacancyDraft } from '../components/VacancySheet.tsx'
import { listRecent, remember } from '../history.ts'
import { VACANCY_TEXT_MIN_LENGTH } from '../limits.ts'
import { SAMPLE_RESUME_URL, SAMPLE_VACANCY } from '../sample.ts'

/** Where a comparison starts: a resume, a vacancy, and optionally a GitHub username. */
export function ComparePage() {
  const navigate = useNavigate()
  const [resume, setResume] = useState<Resume | null>(null)
  const [vacancy, setVacancy] = useState<VacancyDraft>({ title: '', text: '' })
  const [gitHubLogin, setGitHubLogin] = useState('')
  const [busy, setBusy] = useState<'sample' | 'compare' | null>(null)
  const [errors, setErrors] = useState<string[]>([])
  const [serverDown, setServerDown] = useState(false)
  const [recent] = useState(() => listRecent())

  useEffect(() => {
    let cancelled = false
    void isServerReady().then((ready) => {
      if (!cancelled) setServerDown(!ready)
    })
    return () => {
      cancelled = true
    }
  }, [])

  async function run(task: 'sample' | 'compare', work: () => Promise<void>) {
    if (busy) return
    setBusy(task)
    setErrors([])
    try {
      await work()
    } catch (error) {
      setErrors(error instanceof ApiError ? error.messages : ['Something went wrong. Try again.'])
    } finally {
      setBusy(null)
    }
  }

  const loadSample = () =>
    run('sample', async () => {
      const file = await fetch(SAMPLE_RESUME_URL).then((response) => response.blob())
      setResume(await uploadResume(new File([file], 'sample-resume.pdf', { type: 'application/pdf' })))
      setVacancy(SAMPLE_VACANCY)
    })

  function compare(event: FormEvent) {
    event.preventDefault()

    const missing = [
      !resume && 'Upload a resume.',
      !vacancy.title.trim() && 'Give the vacancy a job title.',
      vacancy.text.trim().length < VACANCY_TEXT_MIN_LENGTH &&
        `Paste the full vacancy text, at least ${VACANCY_TEXT_MIN_LENGTH} characters.`,
    ].filter((message): message is string => Boolean(message))
    if (missing.length > 0 || !resume) {
      setErrors(missing)
      return
    }

    void run('compare', async () => {
      const saved = await createVacancy(vacancy)
      const analysis = await createAnalysis({
        resumeId: resume.id,
        vacancyId: saved.id,
        gitHubLogin: gitHubLogin.trim() || null,
      })
      remember({ id: analysis.id, title: saved.title, createdAt: analysis.createdAt, score: null })
      await navigate(`/analyses/${analysis.id}`)
    })
  }

  return (
    <PageShell>
      <header className="mt-8 max-w-3xl">
        <h1 className="text-4xl leading-[1.08] font-medium tracking-tight text-balance sm:text-5xl">
          Does your resume prove what the vacancy asks for?
        </h1>
        <p className="mt-5 max-w-[58ch] text-lg text-ink-soft">
          Upload your resume and paste a vacancy. Each requirement is checked against a line in your resume or
          your public code on GitHub, so a match is never a guess.
        </p>
        <p className="mt-4">
          Nothing at hand?{' '}
          <TextButton onClick={() => void loadSample()}>
            {busy === 'sample' ? 'Loading the sample…' : 'Load a sample resume and vacancy'}
          </TextButton>
        </p>
      </header>

      {serverDown && (
        <p role="alert" className="mt-8 rounded-sm border border-fail bg-sheet px-4 py-3 font-medium text-fail">
          Can&apos;t reach the server. Start the API and its database, then reload this page.
        </p>
      )}

      <form onSubmit={compare} noValidate>
        <div className="mt-10 grid items-start gap-4 lg:grid-cols-2">
          <ResumeSheet resume={resume} onChange={setResume} />
          <VacancySheet draft={vacancy} onChange={setVacancy} />
        </div>

        <div className="mt-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div className="max-w-sm grow">
            <label htmlFor="github-login" className="block text-sm font-medium">
              GitHub username <span className="font-normal text-ink-soft">(optional)</span>
            </label>
            <input
              id="github-login"
              value={gitHubLogin}
              onChange={(event) => setGitHubLogin(event.target.value)}
              autoCapitalize="none"
              autoCorrect="off"
              spellCheck={false}
              aria-describedby="github-hint"
              className="mt-1 block w-full rounded-sm border border-rule bg-white px-3 py-2 hover:border-ink"
            />
            <p id="github-hint" className="mt-1 text-sm text-ink-soft">
              Public repositories count as evidence too.
            </p>
          </div>

          <button
            type="submit"
            disabled={busy !== null}
            className="shrink-0 cursor-pointer rounded-sm border border-ink bg-marker px-6 py-3 text-lg font-medium hover:bg-marker-deep disabled:cursor-default disabled:opacity-60"
          >
            {busy === 'compare' ? 'Starting…' : 'Compare'}
          </button>
        </div>
        <ErrorList messages={errors} />
      </form>

      <RecentComparisons comparisons={recent} />
    </PageShell>
  )
}
