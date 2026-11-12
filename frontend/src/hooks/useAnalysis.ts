import { useEffect, useState } from 'react'
import { ApiError, getAnalysis, watchAnalysis, type Analysis } from '../api.ts'

const POLL_INTERVAL_MS = 1500

type State =
  | { phase: 'loading' }
  | { phase: 'ready'; analysis: Analysis }
  | { phase: 'error'; message: string }

const isFinished = (analysis: Pick<Analysis, 'status'>) =>
  analysis.status === 'completed' || analysis.status === 'failed'

/**
 * Loads an analysis and keeps it current until it finishes. Progress arrives over the
 * event stream; if the stream fails, the analysis is polled instead, so a proxy that
 * buffers events slows the progress display down but never breaks it.
 */
export function useAnalysis(id: string): State {
  const [state, setState] = useState<State>({ phase: 'loading' })

  useEffect(() => {
    let cancelled = false
    let stopWatching = () => {}
    let pollTimer: number | undefined

    async function load(): Promise<Analysis | null> {
      try {
        const analysis = await getAnalysis(id)
        if (!cancelled) setState({ phase: 'ready', analysis })
        return analysis
      } catch (error) {
        if (cancelled) return null
        const notFound = error instanceof ApiError && error.status === 404
        setState({
          phase: 'error',
          message: notFound
            ? 'This comparison does not exist. Check the address, or start a new one.'
            : error instanceof ApiError
              ? error.message
              : 'The comparison could not be loaded.',
        })
        return null
      }
    }

    function poll() {
      pollTimer = window.setTimeout(async () => {
        const analysis = await load()
        if (analysis && !isFinished(analysis) && !cancelled) poll()
      }, POLL_INTERVAL_MS)
    }

    void load().then((analysis) => {
      if (!analysis || isFinished(analysis) || cancelled) return

      stopWatching = watchAnalysis(
        id,
        (progress) => {
          if (cancelled) return
          if (isFinished(progress)) {
            // The final event carries only the status; the report comes with a reload.
            void load()
          } else {
            setState((current) =>
              current.phase === 'ready' ? { phase: 'ready', analysis: { ...current.analysis, ...progress } } : current,
            )
          }
        },
        poll,
      )
    })

    return () => {
      cancelled = true
      stopWatching()
      window.clearTimeout(pollTimer)
    }
  }, [id])

  return state
}
