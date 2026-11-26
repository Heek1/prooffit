import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { deleteAnalysis, type Analysis, type AnalysisResult } from '../api.ts'
import { DocumentText } from '../components/DocumentText.tsx'
import { EvidenceThreads } from '../components/EvidenceThreads.tsx'
import { GitHubPanel } from '../components/GitHubPanel.tsx'
import { Notice, PageShell } from '../components/PageShell.tsx'
import { ProgressSteps } from '../components/ProgressSteps.tsx'
import { ProofBar } from '../components/ProofBar.tsx'
import { RequirementEditor } from '../components/RequirementEditor.tsx'
import { RequirementList } from '../components/RequirementList.tsx'
import { Sheet } from '../components/Sheet.tsx'
import { StillToProve } from '../components/StillToProve.tsx'
import { forget, remember } from '../history.ts'
import { useAnalysis } from '../hooks/useAnalysis.ts'
import { useCountUp } from '../hooks/useCountUp.ts'
import { countByStatus, engineSentence, firstWithEvidence, headline, remainder, reportSummary } from '../report.ts'

const linkStyle = 'font-medium underline underline-offset-4 hover:decoration-2'

/** One comparison: its progress while it runs, then the report or the reason it failed. */
export function ReportPage() {
  const { id = '' } = useParams()

  return (
    <PageShell
      action={
        <Link to="/" className={`text-sm print:hidden ${linkStyle}`}>
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
  // The verdict arrives rather than just being there: the count runs up to its number once.
  const shown = useCountUp(countByStatus(result.requirements).confirmed)
  // On a narrow screen the resume sits below the whole list, so choosing a requirement
  // brings the resume up from the bottom edge instead.
  const [sheetOpen, setSheetOpen] = useState(false)

  function choose(id: string) {
    setSelectedId(id)
    setSheetOpen(true)
  }

  const [editing, setEditing] = useState(false)

  // Keeps the score in the list of recent comparisons on the start page.
  useEffect(() => {
    remember({ id: analysis.id, title: analysis.vacancy.title, createdAt: analysis.createdAt, score: result.score })
  }, [analysis.id, analysis.vacancy.title, analysis.createdAt, result.score])

  return (
    <>
      <header className="max-w-4xl">
        <p className="font-medium">{analysis.vacancy.title}</p>
        <h1
          aria-label={headline(result.requirements)}
          className="mt-2 text-4xl leading-[1.05] font-medium tracking-tight text-balance tabular-nums sm:text-6xl"
        >
          {headline(result.requirements, shown)}
        </h1>
        <p className="mt-4 text-lg text-ink-soft">
          {remainder(result.requirements)} Score {result.score} out of 100, with must-haves counting double.
        </p>
        <ExportActions analysis={{ ...analysis, result }} />
      </header>

      <div className="mt-8 max-w-3xl">
        <ProofBar requirements={result.requirements} />
      </div>

      {(result.engineNote || result.gitHubNote || analysis.requirementsEdited) && (
        <div className="mt-8 max-w-3xl space-y-2">
          {analysis.requirementsEdited && (
            <Notice>These requirements were corrected by hand, not read from the vacancy.</Notice>
          )}
          {result.engineNote && <Notice>{result.engineNote}</Notice>}
          {result.gitHubNote && <Notice>GitHub evidence was not used. {result.gitHubNote}</Notice>}
        </div>
      )}

      <div
        className="relative mt-10 grid items-start gap-x-12 gap-y-10 lg:grid-cols-[minmax(0,1fr)_minmax(0,26rem)]"
      >
        <div className="space-y-8">
          {editing ? (
            <RequirementEditor
              analysisId={analysis.id}
              vacancyTitle={analysis.vacancy.title}
              requirements={result.requirements}
              onCancel={() => setEditing(false)}
            />
          ) : (
            <>
              <StillToProve requirements={result.requirements} onSelect={choose} />
              <p className="text-sm text-ink-soft print:hidden">
                Read a requirement wrongly?{' '}
                <button type="button" onClick={() => setEditing(true)} className={`cursor-pointer text-ink ${linkStyle}`}>
                  Edit the requirements
                </button>
              </p>
              <RequirementList
                requirements={result.requirements}
                resumeLines={analysis.resume.text.split('\n')}
                facts={result.gitHub?.facts ?? []}
                selectedId={selectedId}
                onSelect={choose}
              />
            </>
          )}
        </div>

        {/* Printed, the evidence is already quoted under each requirement. */}
        <aside className="space-y-4 lg:sticky lg:top-6 print:hidden" aria-label="Evidence">
          <Sheet title="Resume" detail={analysis.resume.fileName}>
            <DocumentText
              text={analysis.resume.text}
              label="Resume text"
              cited={selected?.resumeLines}
              maxHeight="max-h-88"
            />
          </Sheet>
          {result.gitHub && <GitHubPanel evidence={result.gitHub} cited={selected?.gitHubFacts ?? []} />}
          {/* Where the chosen requirement came from, so the reading of the vacancy can be checked too. */}
          <Sheet title="Vacancy" detail={analysis.vacancy.title}>
            <DocumentText
              text={analysis.vacancy.text}
              label="Vacancy text"
              source={selected?.vacancyLine}
              maxHeight="max-h-44"
            />
          </Sheet>
        </aside>

        <EvidenceThreads selectedId={selectedId} />
      </div>

      <footer className="mt-12 border-t border-rule pt-4 text-sm text-ink-soft">
        <p>{engineSentence(analysis.engine, result.usage)}</p>
        {/* A printed report says where it came from, since the page it was printed from expires. */}
        <p className="hidden print:block">
          Made on {new Date(analysis.createdAt).toLocaleDateString('en', { dateStyle: 'long' })}. Full report, until it is deleted:{' '}
          {window.location.href}
        </p>
        <div className="print:hidden">
          <Lifetime analysis={analysis} />
        </div>
      </footer>

      {sheetOpen && selected && (
        // The phone's evidence sheet is part of the screen, not of the report.
        <>
          {/* Keeps the end of the page reachable above the sheet. */}
          <div className="h-80 lg:hidden print:hidden" aria-hidden="true" />
          <section
            aria-label="Resume lines for the chosen requirement"
            className="slide-up fixed inset-x-0 bottom-0 z-10 print:hidden rounded-t-2xl border-t border-rule bg-sheet shadow-[0_-12px_40px_rgb(29_43_79/0.2)] lg:hidden"
          >
            <div className="mx-auto mt-2 h-1 w-9 rounded-full bg-rule" aria-hidden="true" />
            <header className="flex items-center justify-between gap-4 border-b border-rule px-4 pb-2">
              <div className="min-w-0">
                <h2 className="font-semibold">Resume</h2>
                <p className="truncate text-sm text-ink-soft">
                  {selected.resumeLines.length > 0
                    ? analysis.resume.fileName
                    : 'No line of the resume backs this requirement.'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setSheetOpen(false)}
                className={`min-h-11 shrink-0 cursor-pointer px-2 text-sm ${linkStyle}`}
              >
                Close
              </button>
            </header>
            <div className="px-2 py-3">
              <DocumentText
                text={analysis.resume.text}
                label="Resume text for the chosen requirement"
                cited={selected.resumeLines}
                maxHeight="max-h-56"
              />
            </div>
          </section>
        </>
      )}
    </>
  )
}

/** Says when the comparison disappears, and lets its owner remove it now. */
/** Takes the report away: as text for an email or a note, or as a PDF through the browser's print dialog. */
function ExportActions({ analysis }: { analysis: Analysis & { result: AnalysisResult } }) {
  const [copied, setCopied] = useState<'idle' | 'copied' | 'failed'>('idle')

  async function copy() {
    try {
      await navigator.clipboard.writeText(reportSummary(analysis, window.location.href))
      setCopied('copied')
    } catch {
      setCopied('failed')
    }
  }

  return (
    <div className="mt-5 flex flex-wrap items-center gap-x-5 gap-y-2 text-sm print:hidden">
      <button type="button" onClick={() => void copy()} className={`cursor-pointer ${linkStyle}`}>
        Copy summary
      </button>
      <button type="button" onClick={() => window.print()} className={`cursor-pointer ${linkStyle}`}>
        Save as PDF
      </button>
      <p role="status" className="text-ink-soft">
        {copied === 'copied' ? 'Summary copied.' : copied === 'failed' ? 'The browser did not allow copying.' : ''}
      </p>
    </div>
  )
}

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
