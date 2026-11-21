import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test, type Page, type Request } from '@playwright/test'

const here = path.dirname(fileURLToPath(import.meta.url))
const loginAspPath = path.join(here, '../../samples/login.asp')
const safeOrdersAspPath = path.join(here, '../../samples/safe-orders.asp')

// A multi-line SQL injection sample where the vulnerable line is not line 1.
const sqlInjectionSample = [
  '<%',
  'Dim sql, userName',
  'userName = Request.QueryString("name")',
  'sql = "SELECT * FROM Users WHERE Name = \'" & userName & "\'"',
  '%>',
].join('\n')

const validationErrorText = 'Enter code or upload a file before analyzing.'
const apiErrorText = 'Something went wrong while analyzing the code. Please try again.'

function findingRow(page: Page, ruleId: string) {
  return page.getByTestId('finding-row').filter({ hasText: ruleId })
}

function findingRowAtLine(page: Page, ruleId: string, line: number) {
  return findingRow(page, ruleId).filter({
    has: page.locator('[data-testid="finding-line"]', { hasText: new RegExp(`^${line}$`) }),
  })
}

test('home page shows the code input, file upload and analyze button', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByTestId('code-input')).toBeVisible()
  await expect(page.getByTestId('file-upload')).toBeVisible()
  await expect(page.getByTestId('analyze-button')).toBeVisible()
})

test('pasting a SQL injection sample reports SQL001 at the vulnerable line', async ({ page }) => {
  await page.goto('/')

  await page.getByTestId('code-input').fill(sqlInjectionSample)
  await page.getByTestId('analyze-button').click()

  const row = findingRow(page, 'SQL001')
  await expect(row).toBeVisible()
  await expect(row.getByTestId('finding-line')).toHaveText('4')
})

test('uploading a classic-asp sample detects language and reports its findings', async ({ page }) => {
  await page.goto('/')

  await page.getByTestId('file-upload').setInputFiles(loginAspPath)
  await expect(page.getByTestId('language-select')).toHaveValue('classic-asp')

  await page.getByTestId('analyze-button').click()

  const sqlRowAtLine20 = findingRowAtLine(page, 'SQL001', 20)
  await expect(sqlRowAtLine20).toHaveCount(1)

  const errRowAtLine4 = findingRowAtLine(page, 'ERR001', 4)
  await expect(errRowAtLine4).toHaveCount(1)
})

test('uploading a safe sample reports only Info findings, no SQL001 or XSS001', async ({ page }) => {
  await page.goto('/')

  await page.getByTestId('file-upload').setInputFiles(safeOrdersAspPath)
  await expect(page.getByTestId('language-select')).toHaveValue('classic-asp')

  await page.getByTestId('analyze-button').click()

  await expect(page.getByTestId('findings-table')).toBeVisible()
  await expect(findingRow(page, 'SQL001')).toHaveCount(0)
  await expect(findingRow(page, 'XSS001')).toHaveCount(0)
  await expect(findingRow(page, 'COM001')).toHaveCount(2)
  await expect(findingRow(page, 'INC001')).toHaveCount(1)
})

test('empty input shows a validation error and sends no request', async ({ page }) => {
  await page.goto('/')

  let requestedAnalyses = false
  page.on('request', (request: Request) => {
    if (request.url().includes('/api/analyses')) {
      requestedAnalyses = true
    }
  })

  await page.getByTestId('analyze-button').click()

  await expect(page.getByTestId('validation-error')).toHaveText(validationErrorText)
  expect(requestedAnalyses).toBe(false)
})

test('a 500 response from the API shows a friendly error and keeps the app usable', async ({ page }) => {
  await page.route('**/api/analyses', async (route) => {
    await route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Internal server error' }),
    })
  })

  await page.goto('/')
  await page.getByTestId('code-input').fill(sqlInjectionSample)
  await page.getByTestId('analyze-button').click()

  await expect(page.getByTestId('api-error')).toHaveText(apiErrorText)
  await expect(page.getByTestId('app-title')).toBeVisible()
  await expect(page.getByTestId('analyze-button')).toBeEnabled()
})

test('a network abort on the API shows a friendly error and keeps the app usable', async ({ page }) => {
  await page.route('**/api/analyses', async (route) => {
    await route.abort()
  })

  await page.goto('/')
  await page.getByTestId('code-input').fill(sqlInjectionSample)
  await page.getByTestId('analyze-button').click()

  await expect(page.getByTestId('api-error')).toHaveText(apiErrorText)
  await expect(page.getByTestId('app-title')).toBeVisible()
  await expect(page.getByTestId('analyze-button')).toBeEnabled()
})

test('uploading an unsupported file type shows an upload error', async ({ page }) => {
  await page.goto('/')

  await page.getByTestId('file-upload').setInputFiles({
    name: 'notes.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('just some plain text notes'),
  })

  await expect(page.getByTestId('upload-error')).toBeVisible()
})
