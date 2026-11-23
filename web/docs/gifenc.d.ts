// Minimal types for the parts of gifenc (no bundled types) that readme-images.spec.ts uses.
declare module 'gifenc' {
  type Palette = number[][]
  interface Encoder {
    writeFrame(index: Uint8Array, width: number, height: number, options: { palette: Palette; delay: number }): void
    finish(): void
    bytes(): Uint8Array
  }
  const gifenc: {
    GIFEncoder(): Encoder
    quantize(rgba: Uint8Array | Uint8ClampedArray, maxColors: number): Palette
    applyPalette(rgba: Uint8Array | Uint8ClampedArray, palette: Palette): Uint8Array
  }
  export default gifenc
}
