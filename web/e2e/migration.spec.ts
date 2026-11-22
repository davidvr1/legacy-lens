import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test } from '@playwright/test'

const here = path.dirname(fileURLToPath(import.meta.url))
const loginAspPath = path.join(here, '../../samples/login.asp')

const sample = ['<%', 'Dim x', 'x = 1', '%>'].join('\n')

const finding = {
  ruleId: 'SQL001',
  severity: 'High',
  line: 3,
  title: 'SQL built by string concatenation',
  explanation: 'e',
  fix: 'f',
}

const migration = {
  code: 'namespace Demo;\n\npublic class A\n{\n    public int X => 1;   \n}',
  notes: ['Note one.', 'Note two.'],
  sources: [{ id: 'sql-injection-parameterized-queries', title: 'SQL Injection and Parameterized Queries' }],
  compilesOk: true,
  diagnostics: [] as { id: string; message: string; line: number }[],
  repaired: false,
}

function mockResponse(overrides: Record<string, unknown>) {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    fileName: 'pasted-code',
    language: 'classic-asp',
    findings: [finding],
    summary: null,
    summaryError: null,
    migration,
    migrationError: null,
    ...overrides,
  }
}

async function analyzeWith(page: import('@playwright/test').Page, body: Record<string, unknown>) {
  await page.route('**/api/analyses', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) }),
  )
  await page.goto('/')
  await page.getByTestId('code-input').fill(sample)
  await page.getByTestId('analyze-button').click()
}

test('demo mode shows a compiling migration proposal with sources and notes', async ({ page }) => {
  await page.goto('/')
  await page.getByTestId('file-upload').setInputFiles(loginAspPath)
  await page.getByTestId('analyze-button').click()

  await expect(page.getByTestId('migration-panel')).toBeVisible()
  await expect(page.getByTestId('compiles-badge')).toHaveText('Compiles ✓')
  const code = await page.getByTestId('migration-code').innerText()
  expect(code.trim().length).toBeGreaterThan(0)
  expect(await page.getByTestId('migration-source').count()).toBeGreaterThanOrEqual(1)
  await expect(page.getByTestId('migration-notes')).toBeVisible()
  await expect(page.getByTestId('migration-error')).toHaveCount(0)
})

test('a migration that does not compile shows the badge and each diagnostic', async ({ page }) => {
  await analyzeWith(
    page,
    mockResponse({
      migration: {
        ...migration,
        compilesOk: false,
        diagnostics: [
          { id: 'CS0103', message: "The name 'foo' does not exist", line: 12 },
          { id: 'CS1002', message: '; expected', line: 20 },
        ],
      },
    }),
  )

  await expect(page.getByTestId('compiles-badge')).toHaveText('Does not compile')
  await expect(page.getByTestId('migration-diagnostics')).toBeVisible()
  const items = page.getByTestId('migration-diagnostic')
  await expect(items).toHaveCount(2)
  await expect(items.nth(0)).toContainText("Line 12: CS0103 The name 'foo' does not exist")
  await expect(items.nth(1)).toContainText('Line 20: CS1002 ; expected')
})

test('a compiling migration shows no diagnostics list and no repaired note', async ({ page }) => {
  await analyzeWith(page, mockResponse({}))

  await expect(page.getByTestId('compiles-badge')).toHaveText('Compiles ✓')
  await expect(page.getByTestId('migration-diagnostics')).toHaveCount(0)
  await expect(page.getByTestId('migration-repaired')).toHaveCount(0)
})

test('a repaired migration shows the repaired note', async ({ page }) => {
  await analyzeWith(page, mockResponse({ migration: { ...migration, repaired: true } }))

  await expect(page.getByTestId('migration-repaired')).toBeVisible()
  await expect(page.getByTestId('migration-repaired')).toContainText('repair')
})

test('sources are listed with title and id; empty sources show the empty message', async ({ page }) => {
  await analyzeWith(page, mockResponse({}))
  const source = page.getByTestId('migration-source')
  await expect(source).toHaveCount(1)
  await expect(source).toContainText('SQL Injection and Parameterized Queries')
  await expect(source).toContainText('sql-injection-parameterized-queries')
  await expect(page.getByTestId('migration-sources-empty')).toHaveCount(0)

  await page.unroute('**/api/analyses')
  await page.route('**/api/analyses', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockResponse({ migration: { ...migration, sources: [] } })),
    }),
  )
  await page.getByTestId('analyze-button').click()

  await expect(page.getByTestId('migration-sources-empty')).toHaveText('No knowledge notes were cited.')
  await expect(page.getByTestId('migration-source')).toHaveCount(0)
})

test('notes are rendered as list items', async ({ page }) => {
  await analyzeWith(page, mockResponse({}))

  await expect(page.getByTestId('migration-notes').locator('li')).toHaveText(['Note one.', 'Note two.'])
})

test('a migration error is shown while findings are still displayed', async ({ page }) => {
  const errorText = 'The migration proposal is not available: the language model could not be reached.'
  await analyzeWith(page, mockResponse({ migration: null, migrationError: errorText }))

  await expect(page.getByTestId('migration-error')).toHaveText(errorText)
  await expect(page.getByTestId('migration-code')).toHaveCount(0)
  await expect(page.getByTestId('compiles-badge')).toHaveCount(0)
  await expect(page.getByTestId('findings-table')).toBeVisible()
})

test('a null migration without an error text shows the default message', async ({ page }) => {
  await analyzeWith(page, mockResponse({ migration: null, migrationError: null }))

  await expect(page.getByTestId('migration-error')).toHaveText('The migration proposal is not available.')
})

test('migration-code text equals the mocked code exactly', async ({ page }) => {
  await analyzeWith(page, mockResponse({}))

  await expect(page.getByTestId('migration-code')).toHaveText(migration.code, { useInnerText: false })
  const text = await page.getByTestId('migration-code').textContent()
  expect(text).toBe(migration.code)
})
