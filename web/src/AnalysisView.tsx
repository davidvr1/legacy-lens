import type { AnalysisResult } from './api'
import SummaryPanel from './SummaryPanel'
import MigrationPanel from './MigrationPanel'

interface AnalysisViewProps {
  result: AnalysisResult
}

/** Local date-time, or null when the value is missing or not a valid date. */
function formatDate(value: string | undefined): string | null {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date.toLocaleString()
}

function AnalysisView({ result }: AnalysisViewProps) {
  const createdAt = formatDate(result.createdAt)
  return (
    <div data-testid="analysis-view">
      <div className="analysis-header">
        <span data-testid="analysis-file-name" className="analysis-file-name">
          {result.fileName}
        </span>
        {createdAt && (
          <span data-testid="analysis-created-at" className="analysis-created-at">
            {`Created ${createdAt}`}
          </span>
        )}
      </div>

      <div className="results-section">
        <SummaryPanel summary={result.summary} error={result.summaryError} />
        {result.findings.length === 0 ? (
          <div data-testid="no-findings">No findings for this file.</div>
        ) : (
          <table data-testid="findings-table" className="findings-table">
            <thead>
              <tr>
                <th>Line</th>
                <th>Severity</th>
                <th>Rule</th>
                <th>Title</th>
                <th>Explanation</th>
                <th>Fix</th>
              </tr>
            </thead>
            <tbody>
              {result.findings.map((finding, idx) => (
                <tr key={idx} data-testid="finding-row">
                  <td data-testid="finding-line">{finding.line}</td>
                  <td>
                    <span className={`severity severity-${finding.severity.toLowerCase()}`}>
                      {finding.severity}
                    </span>
                  </td>
                  <td data-testid="finding-rule">{finding.ruleId}</td>
                  <td>{finding.title}</td>
                  <td>{finding.explanation}</td>
                  <td>{finding.fix}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <MigrationPanel
          migration={result.migration ?? null}
          error={result.migrationError ?? null}
        />
      </div>
    </div>
  )
}

export default AnalysisView
