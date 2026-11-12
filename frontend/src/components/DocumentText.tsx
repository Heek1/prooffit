import { useEffect, useRef } from 'react'

type DocumentTextProps = {
  text: string
  label: string
  /** 1-based line numbers to mark as cited evidence. The first one is scrolled into view. */
  cited?: number[]
  /** Tailwind max-height class for the scrolling area. */
  maxHeight?: string
}

/** Document text, one numbered row per line. Scrolls when it is long. */
export function DocumentText({ text, label, cited = [], maxHeight = 'max-h-112' }: DocumentTextProps) {
  const list = useRef<HTMLOListElement>(null)
  const firstCited = cited.length > 0 ? Math.min(...cited) : null

  useEffect(() => {
    if (firstCited === null || !list.current) return
    const row = list.current.children[firstCited - 1] as HTMLElement | undefined
    if (!row) return

    // Scrolls the list itself, never the page: the reader chose a requirement and should
    // not be moved away from it.
    list.current.scrollTo({ top: row.offsetTop - list.current.clientHeight / 3, behavior: 'smooth' })
  }, [firstCited])

  return (
    // tabIndex makes the scrollable area reachable from the keyboard.
    <ol ref={list} aria-label={label} tabIndex={0} className={`document relative overflow-y-auto ${maxHeight}`}>
      {text.split('\n').map((line, index) => (
        <li key={index} className={cited.includes(index + 1) ? 'is-cited' : undefined}>
          {line}
        </li>
      ))}
    </ol>
  )
}
