import { useEffect, useRef } from 'react'
import { trackSectionView } from '../analytics'

export function useTrackSection(sectionId: string): React.RefObject<HTMLElement | null> {
  const ref = useRef<HTMLElement | null>(null)
  const hasFired = useRef(false)

  useEffect(() => {
    const el = ref.current
    if (!el || hasFired.current) return

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && !hasFired.current) {
          hasFired.current = true
          trackSectionView(sectionId)
          observer.disconnect()
        }
      },
      { threshold: 0.3 },
    )

    observer.observe(el)
    return () => observer.disconnect()
  }, [sectionId])

  return ref
}
