import { useId, type ReactNode } from 'react'

type SheetProps = {
  title: string
  /** What the sheet currently holds, such as the file name. */
  detail?: string
  action?: ReactNode
  children: ReactNode
}

/** One of the two documents being compared, drawn as a sheet of paper on the desk. */
export function Sheet({ title, detail, action, children }: SheetProps) {
  const headingId = useId()

  return (
    <section aria-labelledby={headingId} className="rounded-sm border border-rule bg-sheet">
      <header className="flex min-h-16 items-center justify-between gap-4 border-b border-rule px-5 py-2">
        <div className="min-w-0">
          <h2 id={headingId} className="font-semibold">
            {title}
          </h2>
          {detail && <p className="truncate text-sm text-ink-soft">{detail}</p>}
        </div>
        {action}
      </header>
      <div className="p-5">{children}</div>
    </section>
  )
}

export function ErrorList({ messages }: { messages: string[] }) {
  if (messages.length === 0) return null

  return (
    <ul role="alert" className="mt-3 space-y-1 text-sm font-medium text-fail">
      {messages.map((message) => (
        <li key={message}>{message}</li>
      ))}
    </ul>
  )
}

export function TextButton({ onClick, children }: { onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="shrink-0 cursor-pointer px-1 py-2 text-sm font-medium underline underline-offset-4 hover:decoration-2"
    >
      {children}
    </button>
  )
}
