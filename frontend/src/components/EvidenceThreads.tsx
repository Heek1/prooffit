import { useLayoutEffect, useRef, useState } from 'react'

type Thread = { path: string; fromX: number; fromY: number; toX: number; toY: number }

// The evidence has to sit clearly to the right of the requirement for a thread to read as
// a connection. On a single-column layout it sits below, and no threads are drawn.
const MIN_SPAN = 24

/**
 * Draws a thread from the chosen requirement to every piece of evidence it cites, so the
 * claim and its proof are visibly tied together.
 *
 * Place it inside the positioned element that holds both the requirements and the
 * evidence. It finds its ends in that element: the requirement is marked
 * `data-requirement`, each cited line or fact `data-cited`, and a scrolling panel
 * `data-evidence-scroll`. Evidence that has scrolled out of its panel is met at the
 * panel's edge.
 */
export function EvidenceThreads({ selectedId }: { selectedId: string | null }) {
  const svg = useRef<SVGSVGElement>(null)
  const [threads, setThreads] = useState<Thread[]>([])

  useLayoutEffect(() => {
    // The parent is read from this component's own element. A ref handed down by the
    // parent would still be empty here on first render, since children are laid out first.
    const root = svg.current?.parentElement
    if (!root) return

    let frame = 0

    const measure = () => {
      frame = 0
      const origin = root.getBoundingClientRect()
      const row = selectedId ? root.querySelector<HTMLElement>(`[data-requirement="${selectedId}"]`) : null
      if (!row) {
        setThreads([])
        return
      }

      // The thread leaves from the right edge of the row, level with its title.
      const title = (row.querySelector('button') ?? row).getBoundingClientRect()
      const fromX = row.getBoundingClientRect().right - origin.left
      const fromY = title.top + title.height / 2 - origin.top

      const next: Thread[] = []
      for (const target of root.querySelectorAll<HTMLElement>('[data-cited]')) {
        const box = target.getBoundingClientRect()
        const panel = target.closest('[data-evidence-scroll]')?.getBoundingClientRect()
        const centre = box.top + box.height / 2
        const visibleY = panel ? Math.min(Math.max(centre, panel.top + 4), panel.bottom - 4) : centre

        const toX = box.left - origin.left
        const toY = visibleY - origin.top
        if (toX - fromX < MIN_SPAN) continue

        const bend = (toX - fromX) / 2
        next.push({
          path: `M ${fromX} ${fromY} C ${fromX + bend} ${fromY}, ${toX - bend} ${toY}, ${toX} ${toY}`,
          fromX,
          fromY,
          toX,
          toY,
        })
      }
      setThreads(next)
    }

    // Everything that can move an end of a thread: the page, a panel, the window, or the
    // content itself changing height.
    const schedule = () => {
      if (frame === 0) frame = requestAnimationFrame(measure)
    }
    measure()
    window.addEventListener('resize', schedule)
    window.addEventListener('scroll', schedule, { passive: true })
    root.addEventListener('scroll', schedule, { capture: true, passive: true })
    const resize = new ResizeObserver(schedule)
    resize.observe(root)

    return () => {
      cancelAnimationFrame(frame)
      window.removeEventListener('resize', schedule)
      window.removeEventListener('scroll', schedule)
      root.removeEventListener('scroll', schedule, { capture: true })
      resize.disconnect()
    }
  }, [selectedId])

  return (
    <svg
      ref={svg}
      className="pointer-events-none absolute inset-0 h-full w-full overflow-visible"
      aria-hidden="true"
    >
      {/* Keyed by requirement so each thread is drawn in once per choice, not on every scroll. */}
      <g key={selectedId} fill="none" stroke="var(--color-ink)" strokeWidth="1.5" strokeLinecap="round">
        {threads.map((thread, index) => (
          <path key={index} d={thread.path} pathLength={1} className="thread" />
        ))}
      </g>
      <g fill="var(--color-ink)">
        {threads.length > 0 && <circle cx={threads[0].fromX} cy={threads[0].fromY} r="3" />}
        {threads.map((thread, index) => (
          <circle key={index} cx={thread.toX} cy={thread.toY} r="3" />
        ))}
      </g>
    </svg>
  )
}
