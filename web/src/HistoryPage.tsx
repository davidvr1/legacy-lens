import { useEffect, useState } from 'react'
import type { AnalysisListItem, AnalysisResult, Language } from './api'
import { getAnalysis, listAnalyses } from './api'
import AnalysisView from './AnalysisView'

type ListState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; items: AnalysisListItem[] }

type DetailState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; result: AnalysisResult }

const languageLabels: Record<Language, string> = {
  'classic-asp': 'Classic ASP',
  webforms: 'WebForms',
}

function HistoryPage() {
  const [list, setList] = useState<ListState>({ status: 'loading' })
  const [detail, setDetail] = useState<DetailState | null>(null)

  useEffect(() => {
    listAnalyses()
      .then((items) => setList({ status: 'ready', items }))
      .catch(() => setList({ status: 'error' }))
  }, [])

  const handleOpen = async (id: string) => {
    setDetail({ status: 'loading' })
    try {
      setDetail({ status: 'ready', result: await getAnalysis(id) })
    } catch {
      // Details are never shown to the user; the API error message is for developers.
      setDetail({ status: 'error' })
    }
  }

  if (detail !== null) {
    return (
      <section data-testid="history-detail" className="history-detail">
        <button
          type="button"
          data-testid="history-back"
          className="history-back-button"
          onClick={() => setDetail(null)}
        >
          Back to history
        </button>

        {detail.status === 'loading' && (
          <div data-testid="history-detail-loading" className="history-loading">
            Loading analysis…
          </div>
        )}

        {detail.status === 'error' && (
          <div data-testid="history-detail-error" className="error">
            Could not load this analysis. Please try again.
          </div>
        )}

        {detail.status === 'ready' && <AnalysisView result={detail.result} />}
      </section>
    )
  }

  return (
    <section data-testid="history-page" className="history-page">
      <h2>History</h2>

      {list.status === 'loading' && (
        <div data-testid="history-loading" className="history-loading">
          Loading history…
        </div>
      )}

      {list.status === 'error' && (
        <div data-testid="history-error" className="error">
          Could not load the history. Please try again.
        </div>
      )}

      {list.status === 'ready' && list.items.length === 0 && (
        <div data-testid="history-empty" className="history-empty">
          No analyses yet. Analyze a file to see it here.
        </div>
      )}

      {list.status === 'ready' && list.items.length > 0 && (
        <table data-testid="history-table" className="history-table">
          <thead>
            <tr>
              <th>File</th>
              <th>Language</th>
              <th>Created</th>
              <th>Findings</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {list.items.map((item) => (
              <tr key={item.id} data-testid="history-row">
                <td data-testid="history-file-name">{item.fileName}</td>
                <td>{languageLabels[item.language]}</td>
                <td>{new Date(item.createdAt).toLocaleString()}</td>
                <td data-testid="history-counts">
                  {`High ${item.findingCounts.high} · Medium ${item.findingCounts.medium} · Low ${item.findingCounts.low} · Info ${item.findingCounts.info}`}
                </td>
                <td>
                  <button
                    type="button"
                    data-testid="history-open"
                    className="history-open-button"
                    onClick={() => handleOpen(item.id)}
                  >
                    Open
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}

export default HistoryPage
