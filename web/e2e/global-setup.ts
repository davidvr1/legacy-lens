// Runs once after the web servers are up and before any test. The first analysis on a cold API
// is slow (Roslyn and EF Core load on first use), which made early tests flaky against the
// default 5 s expect timeout. One warm-up analysis here takes that cost out of the tests.
const apiUrl = 'http://localhost:5080/api/analyses'

export default async function globalSetup() {
  const response = await fetch(apiUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      fileName: 'e2e-warmup.asp',
      language: 'classic-asp',
      code: '<%\nResponse.Write "warm-up"\n%>',
    }),
    signal: AbortSignal.timeout(120_000),
  })
  if (!response.ok) {
    throw new Error(`API warm-up failed: ${response.status} ${await response.text()}`)
  }
}
