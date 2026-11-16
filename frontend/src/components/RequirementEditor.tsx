import { useId, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { ApiError, rerunAnalysis, type Importance, type RequirementMatch } from '../api.ts'
import { remember } from '../history.ts'
import { ErrorList } from './Sheet.tsx'

const MAX_REQUIREMENTS = 40
const MAX_LENGTH = 300

type Draft = { key: number; text: string; importance: Importance; vacancyLine: number | null }

type RequirementEditorProps = {
  analysisId: string
  vacancyTitle: string
  requirements: RequirementMatch[]
  onCancel: () => void
}

const field = 'block w-full rounded-sm border border-field bg-white px-3 py-2 hover:border-ink'

/**
 * Lets the user correct how the vacancy was read: reword, drop, add, or change whether a
 * requirement is a must-have. The documents are then compared again against that list.
 */
export function RequirementEditor({ analysisId, vacancyTitle, requirements, onCancel }: RequirementEditorProps) {
  const navigate = useNavigate()
  const headingId = useId()
  const [nextKey, setNextKey] = useState(requirements.length)
  const [drafts, setDrafts] = useState<Draft[]>(() =>
    requirements.map((r, index) => ({ key: index, text: r.text, importance: r.importance, vacancyLine: r.vacancyLine })),
  )
  const [saving, setSaving] = useState(false)
  const [errors, setErrors] = useState<string[]>([])

  const update = (key: number, change: Partial<Draft>) =>
    setDrafts((current) => current.map((draft) => (draft.key === key ? { ...draft, ...change } : draft)))

  function add() {
    setDrafts((current) => [...current, { key: nextKey, text: '', importance: 'mustHave', vacancyLine: null }])
    setNextKey((key) => key + 1)
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (saving) return

    const kept = drafts.filter((draft) => draft.text.trim().length > 0)
    if (kept.length === 0) {
      setErrors(['Keep at least one requirement.'])
      return
    }

    setSaving(true)
    setErrors([])
    try {
      const started = await rerunAnalysis(
        analysisId,
        kept.map(({ text, importance, vacancyLine }) => ({ text: text.trim(), importance, vacancyLine })),
      )
      remember({ id: started.id, title: `${vacancyTitle} (edited)`, createdAt: started.createdAt, score: null })
      await navigate(`/analyses/${started.id}`)
    } catch (error) {
      setErrors(error instanceof ApiError ? error.messages : ['The comparison could not be started. Try again.'])
      setSaving(false)
    }
  }

  return (
    <form onSubmit={submit} aria-labelledby={headingId} className="border-l-4 border-ink bg-sheet px-5 py-4">
      <h2 id={headingId} className="font-semibold">
        Edit requirements
      </h2>
      <p className="mt-1 text-sm text-ink-soft">
        Fix how the vacancy was read, then compare again. The resume and GitHub evidence are checked the same way.
      </p>

      <ol className="mt-4 space-y-2">
        {drafts.map((draft, index) => (
          <li key={draft.key} className="grid grid-cols-[minmax(0,1fr)_auto_auto] items-center gap-2">
            <input
              value={draft.text}
              onChange={(event) => update(draft.key, { text: event.target.value })}
              maxLength={MAX_LENGTH}
              aria-label={`Requirement ${index + 1}`}
              className={field}
            />
            <select
              value={draft.importance}
              onChange={(event) => update(draft.key, { importance: event.target.value as Importance })}
              aria-label={`Importance of requirement ${index + 1}`}
              className="rounded-sm border border-field bg-white px-2 py-2 text-sm hover:border-ink"
            >
              <option value="mustHave">Must have</option>
              <option value="niceToHave">Nice to have</option>
            </select>
            <button
              type="button"
              onClick={() => setDrafts((current) => current.filter((item) => item.key !== draft.key))}
              aria-label={`Remove requirement ${index + 1}`}
              className="cursor-pointer px-2 py-2 text-sm font-medium underline underline-offset-4 hover:decoration-2"
            >
              Remove
            </button>
          </li>
        ))}
      </ol>

      <div className="mt-4 flex flex-wrap items-center gap-x-4 gap-y-3">
        <button
          type="button"
          onClick={add}
          disabled={drafts.length >= MAX_REQUIREMENTS}
          className="cursor-pointer text-sm font-medium underline underline-offset-4 hover:decoration-2 disabled:cursor-default disabled:opacity-50"
        >
          Add a requirement
        </button>
        <span className="grow" />
        <button
          type="button"
          onClick={onCancel}
          disabled={saving}
          className="cursor-pointer px-2 py-2 font-medium underline underline-offset-4 hover:decoration-2"
        >
          Cancel
        </button>
        <button
          type="submit"
          disabled={saving}
          className="cursor-pointer rounded-sm border border-ink bg-marker px-5 py-2 font-medium hover:bg-marker-deep disabled:cursor-default disabled:opacity-60"
        >
          {saving ? 'Starting…' : 'Compare again'}
        </button>
      </div>
      <ErrorList messages={errors} />
    </form>
  )
}
