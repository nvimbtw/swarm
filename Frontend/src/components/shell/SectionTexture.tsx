import { useId } from 'react'

const GLYPHS = ['.', ':', '/', '\\', '|', '+', '*', '=', '-', '~', '·', ' ']
const CELLS = 12
const WIDTH = 84
const HEIGHT = 28

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
    <svg
      className="pointer-events-none absolute inset-0 h-full w-full"
      style={{ color, opacity }}
      aria-hidden="true"
      focusable="false"
    >
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
  )
}
