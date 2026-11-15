import { useEffect, useState } from 'react'

const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

/**
 * Counts from zero up to a number once, when it first appears, easing out at the end.
 * Someone who asked their system for less motion gets the number at once.
 */
export function useCountUp(target: number, durationMs = 900): number {
  const [value, setValue] = useState(() => (reducedMotion() ? target : 0))

  useEffect(() => {
    if (reducedMotion()) return
    const startedAt = performance.now()
    let frame = 0
    const tick = (now: number) => {
      const progress = Math.min(1, (now - startedAt) / durationMs)
      setValue(Math.round(target * (1 - (1 - progress) ** 3)))
      if (progress < 1) frame = requestAnimationFrame(tick)
    }
    frame = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(frame)
  }, [target, durationMs])

  return value
}
