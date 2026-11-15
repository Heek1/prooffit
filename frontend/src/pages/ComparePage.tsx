import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { ApiError, createAnalysis, createVacancy, isServerReady, uploadResume, type Resume } from '../api.ts'
import { PageShell } from '../components/PageShell.tsx'
import { ProofDemo } from '../components/ProofDemo.tsx'
import { RecentComparisons } from '../components/RecentComparisons.tsx'
import { ResumeSheet } from '../components/ResumeSheet.tsx'
import { ErrorList } from '../components/Sheet.tsx'
import { VacancySheet, type VacancyDraft } from '../components/VacancySheet.tsx'
import { listRecent, remember } from '../history.ts'
import { VACANCY_TEXT_MIN_LENGTH } from '../limits.ts'
import { SAMPLE_RESUME_URL, SAMPLE_VACANCY } from '../sample.ts'

const SOURCE_URL = 'https://github.com/Heek1/prooffit'

// The one primary action of a view: marker fill with a hard ink edge under it.
const primaryButton =
  'shrink-0 cursor-pointer rounded-sm border-[1.5px] border-ink bg-marker px-7 py-3.5 text-lg font-medium shadow-edge hover:bg-marker-deep active:translate-y-[3px] active:shadow-none disabled:cursor-default disabled:opacity-60'

/** Where a comparison starts: a resume, a vacancy, and optionally a GitHub username. */
export function ComparePage() {
  const navigate = useNavigate()
  const [resume, setResume] = useState<Resume | null>(null)
  const [vacancy, setVacancy] = useState<VacancyDraft>({ title: '', text: '' })
  const [gitHubLogin, setGitHubLogin] = useState('')
  const [busy, setBusy] = useState<'sample' | 'compare' | null>(null)
  const [errors, setErrors] = useState<string[]>([])
  // Which action the errors belong to, so they are shown next to the button that caused them.
  const [failed, setFailed] = useState<'sample' | 'compare'>('compare')
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
    setFailed(task)
    setErrors([])
    try {
      await work()
    } catch (error) {
      setErrors(error instanceof ApiError ? error.messages : ['Something went wrong. Try again.'])
    } finally {
      setBusy(null)
    }
  }

  /** Saves the vacancy, starts the comparison and opens its report. */
  async function start(from: Resume, draft: VacancyDraft, login: string | null) {
    const saved = await createVacancy(draft)
    const analysis = await createAnalysis({ resumeId: from.id, vacancyId: saved.id, gitHubLogin: login })
    remember({ id: analysis.id, title: saved.title, createdAt: analysis.createdAt, score: null })
    await navigate(`/analyses/${analysis.id}`)
  }

  // One click from the start page to a report: the sample is uploaded and compared at once.
  const runSample = () =>
    run('sample', async () => {
      const file = await fetch(SAMPLE_RESUME_URL).then((response) => response.blob())
      const sample = await uploadResume(new File([file], 'sample-resume.pdf', { type: 'application/pdf' }))
      await start(sample, SAMPLE_VACANCY, null)
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
      setFailed('compare')
      setErrors(missing)
      return
    }

    void run('compare', () => start(resume, vacancy, gitHubLogin.trim() || null))
  }

  return (
    <PageShell
      action={
        <a href={SOURCE_URL} className="text-sm font-medium underline underline-offset-4 hover:decoration-2">
          Source on GitHub
        </a>
      }
    >
      <header className="mt-10 sm:mt-16">
        <h1 className="text-[2.75rem] leading-[1.04] font-medium tracking-tight text-balance sm:text-6xl lg:text-[5rem]">
          Does your resume <span className="marked">prove</span> what the vacancy asks for?
        </h1>
      </header>

      <div className="mt-10 grid items-start gap-x-16 gap-y-10 lg:grid-cols-[minmax(0,4fr)_minmax(0,6fr)]">
        <div>
          <p className="max-w-[46ch] text-xl leading-relaxed font-light text-ink-soft">
            Each requirement is checked against a line in your resume or your public code on GitHub, so a match
            is never a guess. Your documents and the report are deleted after 24 hours.
          </p>
          <div className="mt-8 flex flex-col items-start gap-4">
            <button
              type="button"
              onClick={() => void runSample()}
              disabled={busy !== null}
              className={primaryButton}
            >
              {busy === 'sample' ? 'Starting the sample…' : 'Run a sample comparison'}
            </button>
            <a href="#own" className="font-medium underline underline-offset-4 hover:decoration-2">
              or upload your own resume below
            </a>
          </div>
          {failed === 'sample' && <ErrorList messages={errors} />}
        </div>

        <ProofDemo />
      </div>

      {serverDown && (
        <p role="status" className="mt-10 rounded-sm border border-rule bg-sheet px-4 py-3 text-ink-soft">
          The server is waking up or can&apos;t be reached. On free hosting the first request can take up to a
          minute. If this message stays, reload the page.
        </p>
      )}

      <form id="own" onSubmit={compare} noValidate className="mt-20 scroll-mt-8">
        <h2 className="text-3xl font-medium tracking-tight">Compare your own resume</h2>
        <div className="mt-6 grid items-start gap-4 lg:grid-cols-2">
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
              className="mt-1 block w-full rounded-sm border border-field bg-white px-3 py-2 hover:border-ink"
            />
            <p id="github-hint" className="mt-1 text-sm text-ink-soft">
              Public repositories count as evidence too.
            </p>
          </div>

          <button
            type="submit"
            disabled={busy !== null}
            className={primaryButton}
          >
            {busy === 'compare' ? 'Starting…' : 'Compare'}
          </button>
        </div>
        {failed === 'compare' && <ErrorList messages={errors} />}
      </form>

      <RecentComparisons comparisons={recent} />

      <footer className="mt-16 flex flex-col gap-3 border-t border-rule pt-4 text-sm text-ink-soft sm:flex-row sm:justify-between">
        <p className="max-w-[70ch]">
          The matcher proposes a verdict. Plain code then checks every cited line and computes the score.
        </p>
        <a href={SOURCE_URL} className="shrink-0 font-medium text-ink underline underline-offset-4 hover:decoration-2">
          Source on GitHub
        </a>
      </footer>
    </PageShell>
  )
}
