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

function mockResponse(overrides: Record<string, unknown>) {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    fileName: 'pasted-code',
    language: 'classic-asp',
    findings: [finding],
    summary: null,
    summaryError: null,
    ...overrides,
  }
}

test('demo mode shows the AI summary panel next to the findings', async ({ page }) => {
  await page.goto('/')
  await page.getByTestId('file-upload').setInputFiles(loginAspPath)
  await page.getByTestId('analyze-button').click()

  await expect(page.getByTestId('summary-panel')).toBeVisible()
  await expect(page.getByTestId('ai-badge')).toBeVisible()
  await expect(page.getByTestId('summary-purpose')).toBeVisible()
  await expect(page.getByTestId('summary-inputs')).toContainText('QueryString: name')
  await expect(page.getByTestId('summary-database')).toContainText('SELECT from Users by name')
  await expect(page.getByTestId('summary-includes')).toContainText('includes/db.inc')
  await expect(page.getByTestId('summary-error')).toHaveCount(0)
  await expect(page.getByTestId('findings-table')).toBeVisible()
})

test('a custom summary renders exact items and "None" for an empty list', async ({ page }) => {
  await page.route('**/api/analyses', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(
        mockResponse({
          summary: {
            purpose: 'Custom purpose text.',
            inputs: ['Form: alpha', 'Form: beta'],
            outputs: ['HTML table'],
            databaseAccess: [],
            includes: ['includes/x.inc'],
            comObjects: ['Scripting.FileSystemObject'],
          },
        }),
      ),
    }),
  )

  await page.goto('/')
  await page.getByTestId('code-input').fill(sample)
  await page.getByTestId('analyze-button').click()

  await expect(page.getByTestId('summary-purpose')).toHaveText('Custom purpose text.')
  const inputs = page.getByTestId('summary-inputs').locator('li')
  await expect(inputs).toHaveText(['Form: alpha', 'Form: beta'])
  await expect(page.getByTestId('summary-outputs').locator('li')).toHaveText(['HTML table'])
  await expect(page.getByTestId('summary-com-objects').locator('li')).toHaveText([
    'Scripting.FileSystemObject',
  ])
  await expect(page.getByTestId('summary-database').getByTestId('summary-empty')).toHaveText('None')
  await expect(page.getByTestId('summary-empty')).toHaveCount(1)
})

test('a summary error is shown while findings are still displayed', async ({ page }) => {
  const errorText = 'The AI summary is not available: the language model could not be reached.'
  await page.route('**/api/analyses', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockResponse({ summary: null, summaryError: errorText })),
    }),
  )

  await page.goto('/')
  await page.getByTestId('code-input').fill(sample)
  await page.getByTestId('analyze-button').click()

  await expect(page.getByTestId('summary-error')).toHaveText(errorText)
  await expect(page.getByTestId('summary-purpose')).toHaveCount(0)
  await expect(page.getByTestId('findings-table')).toBeVisible()
})

test('a new analysis replaces the previous summary', async ({ page }) => {
  let call = 0
  await page.route('**/api/analyses', (route) => {
    call += 1
    const body =
      call === 1
        ? mockResponse({
            summary: {
              purpose: 'First purpose.',
              inputs: [],
              outputs: [],
              databaseAccess: [],
              includes: [],
              comObjects: [],
            },
          })
        : mockResponse({ summaryError: 'Second call failed.' })
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) })
  })

  await page.goto('/')
  await page.getByTestId('code-input').fill(sample)
  await page.getByTestId('analyze-button').click()
  await expect(page.getByTestId('summary-purpose')).toHaveText('First purpose.')

  await page.getByTestId('analyze-button').click()
  await expect(page.getByTestId('summary-error')).toHaveText('Second call failed.')
  await expect(page.getByTestId('summary-purpose')).toHaveCount(0)
})
