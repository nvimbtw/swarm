/** A frame is plain text, or text plus a colour key per cell. */
export type AsciiFrame = string | { text: string; color: string }

export interface AsciiPiece {
  meta: { slug: string; name: string; cols: number; rows: number; fps: number }
  /** Returns a draw function from seconds to a frame. */
  default: () => (t: number) => AsciiFrame
}

export const matrixRain: AsciiPiece
