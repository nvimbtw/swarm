import { act, render } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Companion } from './Companion'
import { COMPANION_ART } from './companion-frames'

describe('Companion', () => {
  afterEach(() => vi.useRealTimers())

  it('keeps every frame of a set the same size so it never jitters', () => {
    for (const [set, pixels] of [['large', 32], ['small', 16]] as const) {
      for (const { palette, frames } of Object.values(COMPANION_ART[set])) {
        expect(frames.length).toBeGreaterThan(1)
        for (const frame of frames) {
          const lines = frame.split('\n')
          expect(lines).toHaveLength(pixels)
          for (const line of lines) expect(line).toHaveLength(pixels)
          // Every key points at a real palette colour.
          for (const key of new Set(frame.replace(/\s/g, ''))) {
            expect(palette['0123456789abcdefghijklmnopqrstuvwxyz'.indexOf(key)]).toMatch(/^#[0-9a-f]{6}$/)
          }
        }
      }
    }
  })

  it('plays the frames for its size', () => {
    vi.useFakeTimers()
    const { container } = render(<Companion pose="generating" size={88} />)
    const canvas = container.querySelector('canvas')!
    expect(canvas).toHaveAttribute('aria-hidden', 'true')
    // The large set: one canvas pixel per character.
    expect(canvas.width).toBe(32)
    expect(canvas).toHaveAttribute('data-frame', '0')
    act(() => vi.advanceTimersByTime(1000 / 6))
    expect(canvas).toHaveAttribute('data-frame', '1')
  })
})
