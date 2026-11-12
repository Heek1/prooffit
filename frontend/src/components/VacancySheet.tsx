import { useState, type FormEvent } from 'react'
import { ApiError, createVacancy, type Vacancy } from '../api.ts'
import {
  VACANCY_TEXT_MAX_LENGTH,
  VACANCY_TEXT_MIN_LENGTH,
  VACANCY_TITLE_LENGTH,
} from '../limits.ts'
import { DocumentText } from './DocumentText.tsx'
import { ErrorList, Sheet, TextButton } from './Sheet.tsx'

type VacancySheetProps = {
  vacancy: Vacancy | null
  onChange: (vacancy: Vacancy | null) => void
}

const field =
  'mt-1 block w-full rounded-sm border border-rule bg-white px-3 py-2 text-base font-normal hover:border-ink'

export function VacancySheet({ vacancy, onChange }: VacancySheetProps) {
  // The draft outlives a save, so "Edit" brings back what was typed.
  const [title, setTitle] = useState('')
  const [text, setText] = useState('')
  const [saving, setSaving] = useState(false)
  const [errors, setErrors] = useState<string[]>([])

  async function save(event: FormEvent) {
    event.preventDefault()
    if (saving) return

    if (text.trim().length < VACANCY_TEXT_MIN_LENGTH) {
      setErrors([`Paste the full vacancy text, at least ${VACANCY_TEXT_MIN_LENGTH} characters.`])
      return
    }

    setSaving(true)
    setErrors([])
    try {
      onChange(await createVacancy({ title, text }))
    } catch (error) {
      setErrors(error instanceof ApiError ? error.messages : ['Saving failed. Try again.'])
    } finally {
      setSaving(false)
    }
  }

  if (vacancy) {
    return (
      <Sheet
        title="Vacancy"
        detail={vacancy.title}
        action={<TextButton onClick={() => onChange(null)}>Edit</TextButton>}
      >
        <DocumentText text={vacancy.text} label="Vacancy text" />
      </Sheet>
    )
  }

  return (
    <Sheet title="Vacancy">
      <form onSubmit={save}>
        <label className="block text-sm font-medium">
          Job title
          <input
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            required
            maxLength={VACANCY_TITLE_LENGTH}
            className={field}
          />
        </label>

        {/* The hint sits outside the label so it is read as a description, not as the name. */}
        <label htmlFor="vacancy-text" className="mt-4 block text-sm font-medium">
          Vacancy text
        </label>
        <p id="vacancy-hint" className="text-sm text-ink-soft">
          Paste the whole posting: responsibilities, requirements, nice-to-haves.
        </p>
        <textarea
          id="vacancy-text"
          value={text}
          onChange={(event) => setText(event.target.value)}
          required
          maxLength={VACANCY_TEXT_MAX_LENGTH}
          rows={9}
          aria-describedby="vacancy-hint vacancy-length"
          className={`${field} resize-y font-serif leading-relaxed`}
        />

        <div className="mt-3 flex items-center justify-between gap-4">
          <p id="vacancy-length" className="text-sm text-ink-soft tabular-nums">
            {text.length.toLocaleString('en')} of {VACANCY_TEXT_MAX_LENGTH.toLocaleString('en')}{' '}
            characters
          </p>
          <button
            type="submit"
            disabled={saving}
            className="shrink-0 cursor-pointer rounded-sm border border-ink bg-marker px-5 py-2 font-medium hover:bg-marker-deep disabled:cursor-default disabled:opacity-60"
          >
            {saving ? 'Saving…' : 'Save vacancy'}
          </button>
        </div>
        <ErrorList messages={errors} />
      </form>
    </Sheet>
  )
}
