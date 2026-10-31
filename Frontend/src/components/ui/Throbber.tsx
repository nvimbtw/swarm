interface Dot {
  x: number
  y: number
}

const ROWS = [3, 4, 5, 4, 3]

function buildDots(): Dot[] {
  const dots: Dot[] = []
  ROWS.forEach((count, row) => {
    const y = 2.5 + row * 5
    const start = 14 - ((count - 1) * 6) / 2
    for (let i = 0; i < count; i += 1) dots.push({ x: start + i * 6, y })
  })
  return dots
}

function buildTree(dots: Dot[]) {
  const adjacency = new Map<number, number[]>()
  dots.forEach((_, index) => adjacency.set(index, []))
  for (let a = 0; a < dots.length; a += 1) {
    for (let b = a + 1; b < dots.length; b += 1) {
      const distance = Math.hypot(dots[a].x - dots[b].x, dots[a].y - dots[b].y)
      if (distance <= 6.2) {
        adjacency.get(a)?.push(b)
        adjacency.get(b)?.push(a)
      }
    }
  }

  // A small seeded generator keeps the tree re-growable rather than merely random.
  let seed = 19
  const random = () => {
    seed = (seed * 1664525 + 1013904223) >>> 0
    return seed / 2 ** 32
  }

  const root = 11
  const visited = new Set<number>([root])
  const queue: Array<{ index: number; depth: number }> = [{ index: root, depth: 0 }]
  const edges: Array<{ a: number; b: number; depth: number }> = []
  const lit = new Set<number>([root])

  while (queue.length > 0) {
    const current = queue.shift()
    if (!current) break
    const neighbours = [...(adjacency.get(current.index) ?? [])].sort(() => random() - 0.5)
    for (const next of neighbours) {
      if (visited.has(next)) continue
      visited.add(next)
      edges.push({ a: current.index, b: next, depth: current.depth + 1 })
      lit.add(next)
      queue.push({ index: next, depth: current.depth + 1 })
    }
  }

  return { edges, lit }
}

const DOTS = buildDots()
const TREE = buildTree(DOTS)

/**
 * A trellis figure, not an icon: nineteen dots on a hexagonal field with a tree
 * grown across it. Fixed 28x25 footprint; callers cannot resize it.
 */
export function Throbber({ className }: { className?: string }) {
  return (
    <svg
      width="28"
      height="25"
      viewBox="0 0 28 25"
      className={className}
      aria-hidden="true"
      focusable="false"
    >
      <g>
        {TREE.edges.map((edge, index) => (
          <line
            key={`${edge.a}-${edge.b}`}
            x1={DOTS[edge.a].x}
            y1={DOTS[edge.a].y}
            x2={DOTS[edge.b].x}
            y2={DOTS[edge.b].y}
            stroke="var(--throbber-spark)"
            strokeWidth="1"
            strokeLinecap="round"
            className="throbber-edge"
            style={{ animationDelay: `${(index % 9) * 120}ms` }}
          />
        ))}
      </g>
      <g>
        {DOTS.map((dot, index) => (
          <circle
            key={`${dot.x}-${dot.y}`}
            cx={dot.x}
            cy={dot.y}
            r={TREE.lit.has(index) ? 1.35 : 1}
            fill={TREE.lit.has(index) ? 'var(--throbber-spark)' : 'var(--throbber-base)'}
            className={TREE.lit.has(index) ? 'throbber-dot' : undefined}
            style={
              TREE.lit.has(index)
                ? { animationDelay: `${(index % 7) * 160}ms` }
                : undefined
            }
          />
        ))}
      </g>
    </svg>
  )
}
