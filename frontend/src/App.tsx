import { useEffect, useState } from 'react'
import { isServerReady, type Resume, type Vacancy } from './api.ts'
import { ResumeSheet } from './components/ResumeSheet.tsx'
import { VacancySheet } from './components/VacancySheet.tsx'

export default function App() {
  const [resume, setResume] = useState<Resume | null>(null)
  const [vacancy, setVacancy] = useState<Vacancy | null>(null)
  const [serverDown, setServerDown] = useState(false)

  useEffect(() => {
    let cancelled = false
    void isServerReady().then((ready) => {
      if (!cancelled) setServerDown(!ready)
    })
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-8 sm:py-12">
      <header className="max-w-3xl">
        <p className="text-lg font-semibold">ProofFit</p>
        <h1 className="mt-8 text-4xl leading-[1.08] font-medium tracking-tight text-balance sm:text-5xl">
          Does your resume prove what the vacancy asks for?
        </h1>
        <p className="mt-5 max-w-[58ch] text-lg text-ink-soft">
          Upload your resume and paste a vacancy. Each requirement is checked against a line in
          your resume, so a match is never a guess.
        </p>
      </header>

      {serverDown && (
        <p role="alert" className="mt-8 rounded-sm border border-fail bg-sheet px-4 py-3 font-medium text-fail">
          Can&apos;t reach the server. Start the API and its database, then reload this page.
        </p>
      )}

      <main className="mt-10 grid items-start gap-4 lg:grid-cols-2">
        <ResumeSheet resume={resume} onChange={setResume} />
        <VacancySheet vacancy={vacancy} onChange={setVacancy} />
      </main>

      <p role="status" className="mt-6 text-ink-soft">
        {resume && vacancy
          ? 'Both documents are saved. Matching them line by line is the next part being built.'
          : ''}
      </p>
    </div>
  )
}
