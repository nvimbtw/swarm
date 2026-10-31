import { readFileSync } from 'node:fs'

const css = readFileSync(new URL('../src/styles/tokens.css', import.meta.url), 'utf8')

function block(re, label) {
  const match = css.match(re)
  if (!match) throw new Error(`Could not find ${label} block in tokens.css`)
  const declarations = new Map()
  for (const line of match[1].split(';')) {
    const trimmed = line.trim()
    if (!trimmed.startsWith('--')) continue
    const index = trimmed.indexOf(':')
    declarations.set(trimmed.slice(0, index).trim(), trimmed.slice(index + 1).trim())
  }
  return declarations
}

const palette = block(/:root\s*\{([\s\S]*?)\n\}/, 'palette')
const light = new Map([...palette, ...block(/:root,\s*\.theme-light\s*\{([\s\S]*?)\n\}/, 'light theme')])
const dark = new Map([...palette, ...block(/\.dark\s*\{([\s\S]*?)\n\}/, 'dark theme')])

function resolve(theme, name, depth = 0) {
  if (depth > 12) throw new Error(`Variable loop at ${name}`)
  const key = name.startsWith('--') ? name : `--${name}`
  const value = theme.get(key)
  if (!value) throw new Error(`Missing token ${name}`)
  const ref = value.match(/^var\((--[\w-]+)\)$/)
  return ref ? resolve(theme, ref[1], depth + 1) : value
}

function rgb(hex) {
  const clean = hex.replace('#', '')
  if (!/^[0-9a-f]{6}$/i.test(clean)) throw new Error(`Not a hex: ${hex}`)
  return [0, 2, 4].map((i) => parseInt(clean.slice(i, i + 2), 16) / 255)
}

function luminance(hex) {
  const [r, g, b] = rgb(hex).map((v) =>
    v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4),
  )
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function contrast(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (hi + 0.05) / (lo + 0.05)
}

const textPairs = [
  ['text-primary', 'bg-canvas'],
  ['text-primary', 'bg-surface'],
  ['text-primary', 'bg-raised'],
  ['text-primary', 'bg-selected'],
  ['text-secondary', 'bg-canvas'],
  ['text-secondary', 'bg-surface'],
  ['text-secondary', 'bg-raised'],
  ['text-secondary', 'bg-selected'],
  ['text-muted', 'bg-canvas'],
  ['text-muted', 'bg-surface'],
  ['text-muted', 'bg-raised'],
  ['text-muted', 'bg-sunken'],
  ['text-muted', 'bg-selected'],
  ['text-muted', 'bg-hover'],
  ['action-primary-fg', 'action-primary-bg'],
  ['action-primary-fg', 'action-primary-bg-hover'],
  ['action-primary-fg', 'action-primary-bg-active'],
  ['action-quiet-fg', 'bg-canvas'],
  ['selection-fg', 'selection-bg'],
  ['status-success', 'bg-canvas'],
  ['status-success', 'bg-surface'],
  ['status-warning', 'bg-canvas'],
  ['status-warning', 'bg-surface'],
  ['status-danger', 'bg-canvas'],
  ['status-danger', 'bg-surface'],
  ['status-info', 'bg-canvas'],
  ['state-queued', 'bg-canvas'],
  ['state-queued', 'bg-selected'],
  ['state-in-progress', 'bg-canvas'],
  ['state-in-progress', 'bg-selected'],
  ['state-verification', 'bg-canvas'],
  ['state-verification', 'bg-selected'],
  ['state-closed', 'bg-canvas'],
  ['state-closed', 'bg-selected'],
  ['kind-model', 'bg-canvas'],
  ['kind-chat', 'bg-canvas'],
  ['kind-rl', 'bg-canvas'],
  ['kind-benchmark', 'bg-canvas'],
  ['destructive', 'bg-canvas'],
]

const graphicPairs = [
  ['border-strong', 'bg-canvas'],
  ['border-strong', 'bg-surface'],
  ['border-focus', 'bg-canvas'],
  ['action-primary-bg', 'bg-canvas'],
  ['throbber-spark', 'bg-canvas'],
  ['throbber-spark', 'bg-surface'],
]

const hairlinePairs = [
  ['border-subtle', 'bg-canvas'],
  ['border-subtle', 'bg-surface'],
]

const floors = [
  ['text', textPairs, 4.5],
  ['graphic', graphicPairs, 3.0],
  ['hairline', hairlinePairs, 1.8],
]

let failures = 0

for (const [mode, theme] of [
  ['light', light],
  ['dark', dark],
]) {
  console.log(`\n${mode.toUpperCase()}`)
  for (const [label, pairs, floor] of floors) {
    console.log(`  ${label} floor ${floor}:1`)
    for (const [fg, bg] of pairs) {
      const fgHex = resolve(theme, fg)
      const bgHex = resolve(theme, bg)
      const ratio = contrast(fgHex, bgHex)
      const pass = ratio >= floor
      if (!pass) failures += 1
      console.log(
        `    ${pass ? 'PASS' : 'FAIL'}  ${ratio.toFixed(2).padStart(5)}  ${fg} on ${bg}  (${fgHex} / ${bgHex})`,
      )
    }
  }
}

console.log('\nExcluded by design: --text-faint is decorative only and never carries copy.')

if (failures > 0) {
  console.error(`\n${failures} contrast pair(s) below floor.`)
  process.exit(1)
}

console.log('\nAll required pairs meet their floor.')
