import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { deleteAnalysis, type Analysis, type AnalysisResult } from '../api.ts'
import { DocumentText } from '../components/DocumentText.tsx'
import { EvidenceThreads } from '../components/EvidenceThreads.tsx'
import { GitHubPanel } from '../components/GitHubPanel.tsx'
import { Notice, PageShell } from '../components/PageShell.tsx'
import { ProgressSteps } from '../components/ProgressSteps.tsx'
import { ProofBar } from '../components/ProofBar.tsx'
import { RequirementList } from '../components/RequirementList.tsx'
import { Sheet } from '../components/Sheet.tsx'
import { forget, remember } from '../history.ts'
import { useAnalysis } from '../hooks/useAnalysis.ts'
import { engineSentence, firstWithEvidence, headline, remainder } from '../report.ts'

const linkStyle = 'font-medium underline underline-offset-4 hover:decoration-2'

/** One comparison: its progress while it runs, then the report or the reason it failed. */
export function ReportPage() {
  const { id = '' } = useParams()

  return (
    <PageShell
      action={
        <Link to="/" className={`text-sm ${linkStyle}`}>
          New comparison
        </Link>
      }
    >
      <main className="mt-8">
        {/* Keyed by id so moving between reports starts from a clean state. */}
        <LoadedAnalysis key={id} id={id} />
      </main>
    </PageShell>
  )
}

function LoadedAnalysis({ id }: { id: string }) {
  const state = useAnalysis(id)

  if (state.phase === 'loading') return <p className="text-ink-soft">Loading the comparison…</p>
  if (state.phase === 'error') return <Problem title="This comparison is not available" detail={state.message} />
  return <AnalysisView analysis={state.analysis} />
}

function AnalysisView({ analysis }: { analysis: Analysis }) {
  if (analysis.status === 'failed') {
    return (
      <Problem
        title="The comparison could not be completed"
        detail={analysis.error ?? 'The analysis failed. Try again.'}
      />
    )
  }

  if (analysis.status !== 'completed' || !analysis.result) {
    return (
      <div className="grid items-start gap-x-12 gap-y-10 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]">
        <div className="max-w-xl">
          <h1 className="text-3xl leading-tight font-medium tracking-tight text-balance">
            Comparing your resume with “{analysis.vacancy.title}”
          </h1>
          <div className="mt-8" role="status">
            <ProgressSteps stage={analysis.stage} usesGitHub={analysis.gitHubLogin !== null} />
          </div>
        </div>

        {/* The resume being read, with a band passing over it for as long as the analysis runs. */}
        <div className="scanning">
          <Sheet title="Resume" detail={analysis.resume.fileName}>
            <DocumentText text={analysis.resume.text} label="Resume text" maxHeight="max-h-88" />
          </Sheet>
        </div>
      </div>
    )
  }

  return <Report analysis={analysis} result={analysis.result} />
}

function Report({ analysis, result }: { analysis: Analysis; result: AnalysisResult }) {
  const [selectedId, setSelectedId] = useState(() => firstWithEvidence(result.requirements)?.id ?? null)
  const selected = result.requirements.find((requirement) => requirement.id === selectedId)

  // Keeps the score in the list of recent comparisons on the start page.
  useEffect(() => {
    remember({ id: analysis.id, title: analysis.vacancy.title, createdAt: analysis.createdAt, score: result.score })
  }, [analysis.id, analysis.vacancy.title, analysis.createdAt, result.score])

  return (
    <>
      <header className="max-w-3xl">
        <p className="font-medium">{analysis.vacancy.title}</p>
        <h1 className="mt-2 text-4xl leading-[1.08] font-medium tracking-tight text-balance sm:text-5xl">
          {headline(result.requirements)}
        </h1>
        <p className="mt-4 text-lg text-ink-soft">
          {remainder(result.requirements)} Score {result.score} out of 100, with must-haves counting double.
        </p>
      </header>

      <div className="mt-8 max-w-3xl">
        <ProofBar requirements={result.requirements} />
      </div>

      {(result.engineNote || result.gitHubNote) && (
        <div className="mt-8 max-w-3xl space-y-2">
          {result.engineNote && <Notice>{result.engineNote}</Notice>}
          {result.gitHubNote && <Notice>GitHub evidence was not used. {result.gitHubNote}</Notice>}
        </div>
      )}

      <div
        className="relative mt-10 grid items-start gap-x-12 gap-y-10 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]"
      >
        <RequirementList
          requirements={result.requirements}
          resumeLines={analysis.resume.text.split('\n')}
          facts={result.gitHub?.facts ?? []}
          selectedId={selectedId}
          onSelect={setSelectedId}
        />

        <aside className="space-y-4 lg:sticky lg:top-6" aria-label="Evidence">
          <Sheet title="Resume" detail={analysis.resume.fileName}>
            <DocumentText
              text={analysis.resume.text}
              label="Resume text"
              cited={selected?.resumeLines}
              maxHeight="max-h-88"
            />
          </Sheet>
          {result.gitHub && <GitHubPanel evidence={result.gitHub} cited={selected?.gitHubFacts ?? []} />}
        </aside>

        <EvidenceThreads selectedId={selectedId} />
      </div>

      <footer className="mt-12 border-t border-rule pt-4 text-sm text-ink-soft">
        <p>{engineSentence(analysis.engine, result.usage)}</p>
        <Lifetime analysis={analysis} />
      </footer>
    </>
  )
}

/** Says when the comparison disappears, and lets its owner remove it now. */
function Lifetime({ analysis }: { analysis: Analysis }) {
  const navigate = useNavigate()
  const [step, setStep] = useState<'idle' | 'confirming' | 'deleting' | 'failed'>('idle')
  const expires = new Date(analysis.expiresAt).toLocaleString('en', { dateStyle: 'medium', timeStyle: 'short' })

  async function remove() {
    setStep('deleting')
    try {
      await deleteAnalysis(analysis.id)
      forget(analysis.id)
      await navigate('/')
    } catch {
      setStep('failed')
    }
  }

  return (
    <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-2">
      {step === 'idle' || step === 'failed' ? (
        <>
          <p>This report and its documents are deleted automatically on {expires}.</p>
          <button type="button" onClick={() => setStep('confirming')} className={`cursor-pointer text-ink ${linkStyle}`}>
            Delete now
          </button>
          {step === 'failed' && (
            <p role="alert" className="font-medium text-fail">
              The report could not be deleted. Try again.
            </p>
          )}
        </>
      ) : (
        <>
          <p className="text-ink">Delete this report, the resume and the vacancy? This cannot be undone.</p>
          <button
            type="button"
            onClick={() => void remove()}
            disabled={step === 'deleting'}
            className="cursor-pointer rounded-sm border border-fail px-3 py-1 font-medium text-fail hover:bg-fail hover:text-white disabled:opacity-60"
          >
            {step === 'deleting' ? 'Deleting…' : 'Delete'}
          </button>
          <button
            type="button"
            onClick={() => setStep('idle')}
            disabled={step === 'deleting'}
            className={`cursor-pointer text-ink ${linkStyle}`}
          >
            Keep
          </button>
        </>
      )}
    </div>
  )
}

function Problem({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="max-w-xl">
      <h1 className="text-3xl leading-tight font-medium tracking-tight">{title}</h1>
      <p className="mt-4 text-lg text-ink-soft">{detail}</p>
      <p className="mt-6">
        <Link to="/" className={linkStyle}>
          Start a new comparison
        </Link>
      </p>
    </div>
  )
}
