import { useEffect, useState } from 'react'
import type { MigrationProposal } from './api'

interface MigrationPanelProps {
  migration: MigrationProposal | null
  error: string | null
}

function MigrationPanel({ migration, error }: MigrationPanelProps) {
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!copied) return
    const timer = window.setTimeout(() => setCopied(false), 2000)
    return () => window.clearTimeout(timer)
  }, [copied])

  const handleCopy = async () => {
    if (!migration) return
    try {
      await navigator.clipboard.writeText(migration.code)
      setCopied(true)
    } catch {
      // The clipboard may be unavailable (e.g. insecure context); the code stays selectable.
    }
  }

  return (
    <section data-testid="migration-panel" className="migration-panel">
      <div className="migration-header">
        <h2>Migration proposal</h2>
        <span data-testid="migration-ai-badge" className="ai-badge">
          AI-generated
        </span>
        {migration && (
          <span
            data-testid="compiles-badge"
            className={`badge ${migration.compilesOk ? 'badge-ok' : 'badge-fail'}`}
          >
            {migration.compilesOk ? 'Compiles ✓' : 'Does not compile'}
          </span>
        )}
      </div>

      {migration ? (
        <>
          {migration.repaired && (
            <p data-testid="migration-repaired" className="migration-repaired">
              Fixed after one repair attempt with the compiler errors.
            </p>
          )}

          {migration.diagnostics.length > 0 && (
            <ul data-testid="migration-diagnostics" className="migration-diagnostics">
              {migration.diagnostics.map((diagnostic, idx) => (
                <li key={idx} data-testid="migration-diagnostic">
                  {`Line ${diagnostic.line}: ${diagnostic.id} ${diagnostic.message}`}
                </li>
              ))}
            </ul>
          )}

          <div className="migration-code-wrapper">
            <button
              type="button"
              data-testid="copy-code-button"
              className="copy-code-button"
              onClick={handleCopy}
            >
              {copied ? 'Copied' : 'Copy code'}
            </button>
            <pre data-testid="migration-code" className="migration-code">
              <code>{migration.code}</code>
            </pre>
          </div>

          <div className="migration-list">
            <h3>Notes</h3>
            <ul data-testid="migration-notes">
              {migration.notes.map((note, idx) => (
                <li key={idx}>{note}</li>
              ))}
            </ul>
          </div>

          <div className="migration-list">
            <h3>Knowledge notes cited</h3>
            <ul data-testid="migration-sources">
              {migration.sources.length === 0 ? (
                <li data-testid="migration-sources-empty">No knowledge notes were cited.</li>
              ) : (
                migration.sources.map((source) => (
                  <li key={source.id} data-testid="migration-source">
                    {`${source.title} (${source.id})`}
                  </li>
                ))
              )}
            </ul>
          </div>
        </>
      ) : (
        <div data-testid="migration-error" className="migration-error">
          {error ?? 'The migration proposal is not available.'}
        </div>
      )}

      <p className="summary-note">
        The migration is AI-generated. Review it before use; it is compiled, not run or tested.
      </p>
    </section>
  )
}

export default MigrationPanel
