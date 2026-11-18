import { useContext, useEffect, useId, useRef } from 'react'
import { connectionStatus } from '../../lib/connection-status'
import { DiagnosticsContext } from '../../lib/use-diagnostics'
import { matrixRain } from '../../vendor/ascii-magic'

const GLYPHS = ['.', ':', '/', '\\', '|', '+', '*', '=', '-', '~', '·', ' ']
const CELLS = 12
const WIDTH = 84
const HEIGHT = 28
// Low frame rate: this is ambient motion behind text, not a feature.
const FPS = 10
// Enough copies of the 84×30 piece to cover a wide header or a tall sidebar.
const REPEAT_X = 5
const REPEAT_Y = 4

/** One frame of the rain with its dim backdrop code removed, tiled to cover the area. */
export function rainText(draw: ReturnType<typeof matrixRain.default>, seconds: number) {
  const frame = draw(seconds)
  if (typeof frame === 'string') return frame
  const colors = frame.color.split('\n')
  // Keys 0–2 are the piece's dim backdrop code; keep only the falling drops.
  const lines = frame.text
    .split('\n')
    .map((line, y) => [...line].map((ch, x) => (colors[y][x] > '2' ? ch : ' ')).join('').repeat(REPEAT_X))
  return Array.from({ length: REPEAT_Y }, () => lines.join('\n')).join('\n')
}

/** Falling glyphs. They fall at full speed while the server is live, and slowly when it is not. */
function Rain() {
  const ref = useRef<HTMLPreElement>(null)
  const elapsed = useRef(0)
  const diagnostics = useContext(DiagnosticsContext)
  const live = diagnostics ? connectionStatus(diagnostics).tone === 'live' : false

  useEffect(() => {
    const pre = ref.current
    if (!pre) return
    const draw = matrixRain.default()
    pre.textContent = rainText(draw, elapsed.current)
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return

    let last = performance.now()
    const speed = live ? 1 : 0.25
    const timer = window.setInterval(() => {
      const now = performance.now()
      if (!document.hidden) elapsed.current += ((now - last) / 1000) * speed
      last = now
      pre.textContent = rainText(draw, elapsed.current)
    }, 1000 / FPS)
    return () => window.clearInterval(timer)
  }, [live])

  return (
    <pre
      ref={ref}
      className="absolute inset-0 m-0 overflow-hidden font-mono text-[10px] leading-[14px] select-none"
    />
  )
}

export function SectionTexture({ tone = 'ink' }: { tone?: 'ink' | 'warm' }) {
  const rawId = useId()
  const id = `texture-${rawId.replace(/[^a-zA-Z0-9_-]/g, '')}`
  const color = tone === 'warm' ? 'var(--texture-warm)' : 'var(--texture-ink)'
  const opacity =
    tone === 'warm' ? 'var(--texture-warm-opacity)' : 'var(--texture-opacity)'

  const glyphs = Array.from({ length: CELLS }, (_, row) =>
    Array.from({ length: 6 }, (_, column) => {
      const index = (row * 7 + column * 5 + row * column) % GLYPHS.length
      return {
        key: `${row}-${column}`,
        x: 3 + column * 14,
        y: 11 + row * 14,
        glyph: GLYPHS[index],
      }
    }),
  ).flat()

  return (
    <div
      className="pointer-events-none absolute inset-0 overflow-hidden"
      style={{ color, opacity }}
      aria-hidden="true"
    >
      <Rain />
      <svg className="absolute inset-0 h-full w-full" focusable="false">
        <defs>
          <pattern id={id} width={WIDTH} height={HEIGHT} patternUnits="userSpaceOnUse">
            {glyphs.map(({ key, x, y, glyph }) => (
              <text
                key={key}
                x={x}
                y={y}
                fill="currentColor"
                fontFamily="var(--font-mono)"
                fontSize="10"
                letterSpacing="1"
              >
                {glyph}
              </text>
            ))}
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill={`url(#${id})`} />
      </svg>
    </div>
  )
}
