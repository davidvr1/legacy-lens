import { expect, test, type Page } from '@playwright/test'

const sqlInjectionSample = [
  '<%',
  'Dim sql, userName',
  'userName = Request.QueryString("name")',
  'sql = "SELECT * FROM Users WHERE Name = \'" & userName & "\'"',
  '%>',
].join('\n')

// Exact-path matchers, so the list route never swallows /api/analyses/{id} and vice versa.
const isListUrl = (url: URL) => url.pathname === '/api/analyses'
const isDetailUrl = (url: URL) => /^\/api\/analyses\/[^/]+$/.test(url.pathname)

const mockedItem = {
  id: '11111111-1111-1111-1111-111111111111',
  fileName: 'mocked-file.asp',
  language: 'classic-asp',
  createdAt: '2026-01-02T03:04:05Z',
  findingCounts: { high: 3, medium: 2, low: 1, info: 4 },
}

async function mockList(page: Page, items: unknown[]) {
  await page.route(isListUrl, (route) =>
    route.request().method() === 'GET'
      ? route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(items) })
      : route.continue(),
  )
}

test('an analysis appears in history and can be opened and closed again', async ({ page }, testInfo) => {
  const fileName = `history-${testInfo.project.name}-${Date.now()}.asp`
  await page.goto('/')

  await page.getByTestId('file-upload').setInputFiles({
    name: fileName,
    mimeType: 'text/plain',
    buffer: Buffer.from(sqlInjectionSample),
  })
  await page.getByTestId('analyze-button').click()
  await expect(page.getByTestId('findings-table')).toBeVisible()

  await page.getByTestId('nav-history').click()

  const row = page.getByTestId('history-row').filter({ hasText: fileName })
  await expect(row).toHaveCount(1)
  await expect(row.getByTestId('history-file-name')).toHaveText(fileName)
  await expect(row.getByTestId('history-counts')).toHaveText(/High [1-9]\d*/)

  await row.getByTestId('history-open').click()

  await expect(page.getByTestId('analysis-view')).toBeVisible()
  await expect(page.getByTestId('analysis-file-name')).toHaveText(fileName)
  const sqlRow = page.getByTestId('finding-row').filter({ hasText: 'SQL001' })
  await expect(sqlRow).toHaveCount(1)
  await expect(sqlRow.getByTestId('finding-line')).toHaveText('4')

  await page.getByTestId('history-back').click()
  await expect(page.getByTestId('history-table')).toBeVisible()
  await expect(page.getByTestId('analysis-view')).toHaveCount(0)
})

test('an empty history shows the empty message', async ({ page }) => {
  await mockList(page, [])
  await page.goto('/')
  await page.getByTestId('nav-history').click()

  await expect(page.getByTestId('history-empty')).toBeVisible()
  await expect(page.getByTestId('history-table')).toHaveCount(0)
})

test('a failing history list shows an error and the app keeps working', async ({ page }) => {
  await page.route(isListUrl, (route) =>
    route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Internal server error' }),
    }),
  )
  await page.goto('/')
  await page.getByTestId('nav-history').click()

  await expect(page.getByTestId('history-error')).toBeVisible()
  await expect(page.getByTestId('app-title')).toBeVisible()
  await expect(page.getByTestId('history-table')).toHaveCount(0)

  await page.getByTestId('nav-analyze').click()
  await expect(page.getByTestId('analyze-button')).toBeVisible()
})

test('a mocked row shows its file name and finding counts', async ({ page }) => {
  await mockList(page, [mockedItem])
  await page.goto('/')
  await page.getByTestId('nav-history').click()

  await expect(page.getByTestId('history-row')).toHaveCount(1)
  await expect(page.getByTestId('history-file-name')).toHaveText('mocked-file.asp')
  await expect(page.getByTestId('history-counts')).toHaveText('High 3 · Medium 2 · Low 1 · Info 4')
})

test('a failing detail request shows a detail error and Back returns to the list', async ({ page }) => {
  await mockList(page, [mockedItem])
  await page.route(isDetailUrl, (route) =>
    route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Internal server error' }),
    }),
  )
  await page.goto('/')
  await page.getByTestId('nav-history').click()
  await page.getByTestId('history-open').click()

  await expect(page.getByTestId('history-detail-error')).toBeVisible()
  await expect(page.getByTestId('analysis-view')).toHaveCount(0)
  await expect(page.getByTestId('app-title')).toBeVisible()

  await page.getByTestId('history-back').click()
  await expect(page.getByTestId('history-table')).toBeVisible()
})

test('a 404 for the detail shows a detail error', async ({ page }) => {
  await mockList(page, [mockedItem])
  await page.route(isDetailUrl, (route) =>
    route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'Analysis not found.' }),
    }),
  )
  await page.goto('/')
  await page.getByTestId('nav-history').click()
  await page.getByTestId('history-open').click()

  await expect(page.getByTestId('history-detail-error')).toBeVisible()
})

test('switching Analyze, History, Analyze keeps the typed code', async ({ page }) => {
  await mockList(page, [])
  await page.goto('/')

  await page.getByTestId('code-input').fill(sqlInjectionSample)
  await page.getByTestId('nav-history').click()
  await expect(page.getByTestId('history-empty')).toBeVisible()
  await expect(page.getByTestId('code-input')).toHaveCount(0)

  await page.getByTestId('nav-analyze').click()
  await expect(page.getByTestId('code-input')).toHaveValue(sqlInjectionSample)
})
