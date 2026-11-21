import type { CodeSummary } from './api'

interface SummaryPanelProps {
  summary: CodeSummary | null
  error: string | null
}

interface SummaryListProps {
  label: string
  testId: string
  items: string[]
}

function SummaryList({ label, testId, items }: SummaryListProps) {
  return (
    <div className="summary-list">
      <h3>{label}</h3>
      <ul data-testid={testId}>
        {items.length === 0 ? (
          <li data-testid="summary-empty">None</li>
        ) : (
          items.map((item, idx) => <li key={idx}>{item}</li>)
        )}
      </ul>
    </div>
  )
}

function SummaryPanel({ summary, error }: SummaryPanelProps) {
  return (
    <section data-testid="summary-panel" className="summary-panel">
      <div className="summary-header">
        <h2>Summary</h2>
        <span data-testid="ai-badge" className="ai-badge">
          AI-generated
        </span>
      </div>

      {summary ? (
        <>
          <p data-testid="summary-purpose">{summary.purpose}</p>
          <SummaryList label="Inputs" testId="summary-inputs" items={summary.inputs} />
          <SummaryList label="Outputs" testId="summary-outputs" items={summary.outputs} />
          <SummaryList
            label="Database access"
            testId="summary-database"
            items={summary.databaseAccess}
          />
          <SummaryList label="Includes" testId="summary-includes" items={summary.includes} />
          <SummaryList
            label="COM objects"
            testId="summary-com-objects"
            items={summary.comObjects}
          />
        </>
      ) : (
        <div data-testid="summary-error" className="summary-error">
          {error ?? 'The AI summary is not available.'}
        </div>
      )}

      <p className="summary-note">
        Findings come from deterministic rules. The summary is AI-generated and may be incomplete.
      </p>
    </section>
  )
}

export default SummaryPanel
