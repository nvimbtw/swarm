import { useEffect, useRef, useState } from 'react'
import type { CompanionPose } from '../../data/fixtures'
import { cn } from '../../lib/cn'
import { COMPANION_ART } from './companion-frames'

// Below this the 32-pixel frames blur together, so small slots get the 16-pixel set.
const LARGE_FROM = 64
// The ascii-magic kit's colour keys: each frame character indexes the pose's palette.
const KEYS = '0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ'
const FPS: Record<CompanionPose, number> = {
  generating: 6,
  'request-sent': 4,
  'request-received': 4,
  away: 2,
  completed: 3,
  offline: 1,
}

interface CompanionProps {
  pose: CompanionPose
  size?: number
  className?: string
}

/** Paints one frame, one canvas pixel per character, in the artwork's own colours. */
function paint(canvas: HTMLCanvasElement, frame: string, palette: string[]) {
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  ctx.clearRect(0, 0, canvas.width, canvas.height)
  frame.split('\n').forEach((line, y) => {
    for (let x = 0; x < line.length; x++) {
      if (line[x] === ' ') continue
      ctx.fillStyle = palette[KEYS.indexOf(line[x])]
      ctx.fillRect(x, y, 1, 1)
    }
  })
}

/**
 * The session's body, as pixel art converted from the companion artwork.
 * The pose carries state, so it is decorative here: the state word beside it
 * is what a screen reader announces.
 */
export function Companion({ pose, size = 24, className }: CompanionProps) {
  const { palette, frames } = COMPANION_ART[size >= LARGE_FROM ? 'large' : 'small'][pose]
  const ref = useRef<HTMLCanvasElement>(null)
  const [tick, setTick] = useState(0)
  const index = tick % frames.length

  useEffect(() => {
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return
    const timer = window.setInterval(() => {
      if (!document.hidden) setTick((t) => t + 1)
    }, 1000 / FPS[pose])
    return () => window.clearInterval(timer)
  }, [pose])

  useEffect(() => {
    if (ref.current) paint(ref.current, frames[index], palette)
  }, [frames, index, palette])

  const pixels = frames[0].split('\n').length
  return (
    <canvas
      ref={ref}
      aria-hidden="true"
      data-frame={index}
      width={pixels}
      height={pixels}
      className={cn('shrink-0 select-none', className)}
      style={{ width: size, height: size, imageRendering: 'pixelated' }}
    />
  )
}
