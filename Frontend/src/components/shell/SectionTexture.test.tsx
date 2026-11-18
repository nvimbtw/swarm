import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { matrixRain } from '../../vendor/ascii-magic'
import { rainText, SectionTexture } from './SectionTexture'

describe('SectionTexture', () => {
  it('keeps only the falling drops and tiles them', () => {
    const text = rainText(matrixRain.default(), 3)
    const lines = text.split('\n')
    expect(lines).toHaveLength(30 * 4)
    expect(lines[0]).toHaveLength(84 * 5)
    // The dim backdrop is gone, so most cells are blank but some drops remain.
    const blanks = [...text].filter((ch) => ch === ' ').length
    expect(blanks / text.length).toBeGreaterThan(0.3)
    expect(text.trim()).not.toBe('')
  })

  it('renders hidden from assistive tech without a diagnostics provider', () => {
    const { container } = render(<SectionTexture />)
    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true')
    expect(container.querySelector('pre')?.textContent).not.toBe('')
  })
})
