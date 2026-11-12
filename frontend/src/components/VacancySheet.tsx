import { VACANCY_TEXT_MAX_LENGTH, VACANCY_TITLE_LENGTH } from '../limits.ts'
import { Sheet } from './Sheet.tsx'

export type VacancyDraft = { title: string; text: string }

type VacancySheetProps = {
  draft: VacancyDraft
  onChange: (draft: VacancyDraft) => void
}

const field =
  'mt-1 block w-full rounded-sm border border-rule bg-white px-3 py-2 text-base font-normal hover:border-ink'

/** The vacancy being written. It is saved when the comparison starts, not before. */
export function VacancySheet({ draft, onChange }: VacancySheetProps) {
  return (
    <Sheet title="Vacancy">
      <label className="block text-sm font-medium">
        Job title
        <input
          value={draft.title}
          onChange={(event) => onChange({ ...draft, title: event.target.value })}
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
        value={draft.text}
        onChange={(event) => onChange({ ...draft, text: event.target.value })}
        maxLength={VACANCY_TEXT_MAX_LENGTH}
        rows={11}
        aria-describedby="vacancy-hint vacancy-length"
        className={`${field} resize-y font-serif leading-relaxed`}
      />
      <p id="vacancy-length" className="mt-2 text-sm text-ink-soft tabular-nums">
        {draft.text.length.toLocaleString('en')} of {VACANCY_TEXT_MAX_LENGTH.toLocaleString('en')} characters
      </p>
    </Sheet>
  )
}
