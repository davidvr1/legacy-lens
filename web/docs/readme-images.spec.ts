import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test, type Page } from '@playwright/test'
import gifenc from 'gifenc'
import { PNG } from 'pngjs'

// Produces the README screenshots and GIF from the real app in Demo mode.
// Run with: npm run screenshots   (output: docs/images/ at the repository root)

const here = path.dirname(fileURLToPath(import.meta.url))
const repoRoot = path.resolve(here, '..', '..')
const outDir = path.join(repoRoot, 'docs', 'images')
const samplePath = (name: string) => path.join(repoRoot, 'samples', name)
const sample = samplePath('login.asp')

test.beforeAll(() => mkdirSync(outDir, { recursive: true }))

async function analyze(page: Page, file: string) {
  await page.getByTestId('nav-analyze').click()
  await page.getByTestId('file-upload').setInputFiles(file)
  await page.getByTestId('analyze-button').click()
  await expect(page.getByTestId('findings-table')).toBeVisible()
  await expect(page.getByTestId('migration-panel')).toBeVisible()
}

test('screenshots', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 })
  await page.goto('/')

  // Shared e2e database: analyze three samples, then crop History to these newest rows.
  await analyze(page, samplePath('CustomerSearch.aspx.cs'))
  await analyze(page, samplePath('search.asp'))
  await page.goto('/')
  await page.getByTestId('file-upload').setInputFiles(sample)
  await page.screenshot({ path: path.join(outDir, 'analyze.png') })
  await analyze(page, sample)

  await page.getByTestId('findings-table').screenshot({ path: path.join(outDir, 'findings.png') })
  await page.getByTestId('summary-panel').screenshot({ path: path.join(outDir, 'summary.png') })
  await page.getByTestId('migration-panel').screenshot({ path: path.join(outDir, 'migration.png') })

  await page.getByTestId('nav-history').click()
  const thirdRow = page.getByTestId('history-row').nth(2)
  await expect(thirdRow).toBeVisible()
  const box = await thirdRow.boundingBox()
  if (!box) throw new Error('History row has no bounding box')
  await page.screenshot({
    path: path.join(outDir, 'history.png'),
    clip: { x: 0, y: 0, width: 1280, height: box.y + box.height + 1 },
  })
})

test('demo gif', async ({ page }) => {
  const width = 1024
  const height = 640
  await page.setViewportSize({ width, height })
  const frames: { png: Buffer; delay: number }[] = []
  const capture = async (delay = 1400) => frames.push({ png: await page.screenshot(), delay })

  await page.goto('/')
  await capture()
  await page.getByTestId('file-upload').setInputFiles(sample)
  await capture()
  await page.getByTestId('analyze-button').click()
  await expect(page.getByTestId('migration-panel')).toBeVisible()

  for (const id of ['summary-panel', 'findings-table', 'migration-panel', 'compiles-badge']) {
    await page.getByTestId(id).evaluate((el) => el.scrollIntoView({ block: 'start' }))
    await capture(1800)
  }

  await page.getByTestId('nav-history').click()
  await expect(page.getByTestId('history-row').first()).toBeVisible()
  await capture(2500)

  const gif = gifenc.GIFEncoder()
  for (const frame of frames) {
    const { data } = PNG.sync.read(frame.png)
    const palette = gifenc.quantize(data, 256)
    gif.writeFrame(gifenc.applyPalette(data, palette), width, height, { palette, delay: frame.delay })
  }
  gif.finish()
  writeFileSync(path.join(outDir, 'demo.gif'), gif.bytes())
})
