import type { CompanionPose } from '../../data/fixtures'
import { cn } from '../../lib/cn'

const FILES: Record<CompanionPose, string> = {
  generating: 'generating',
  'request-sent': 'request-sent',
  'request-received': 'request-received',
  away: 'away',
  completed: 'completed',
  offline: 'offline',
}

interface CompanionProps {
  pose: CompanionPose
  size?: number
  className?: string
}

/**
 * The session's body. The pose carries state, so it is decorative here: the
 * state word beside it is what a screen reader announces.
 */
export function Companion({ pose, size = 24, className }: CompanionProps) {
  const base = `${import.meta.env.BASE_URL}companion/${FILES[pose]}`
  return (
    <img
      src={`${base}-128.webp`}
      srcSet={`${base}-64.webp 64w, ${base}-128.webp 128w`}
      sizes={`${size}px`}
      alt=""
      aria-hidden="true"
      width={size}
      height={size}
      draggable={false}
      className={cn('select-none object-contain', className)}
      style={{ width: size, height: size }}
    />
  )
}
