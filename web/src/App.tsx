import { useState, type ChangeEvent } from 'react'
import './App.css'
import type { Language, AnalysisResult } from './api'
import { analyzeCode } from './api'
import { languageFromFileName, MAX_CODE_LENGTH } from './language'
import AnalysisView from './AnalysisView'
import HistoryPage from './HistoryPage'

type View = 'analyze' | 'history'

function App() {
  const [view, setView] = useState<View>('analyze')
  const [code, setCode] = useState('')
  const [fileName, setFileName] = useState('pasted-code')
  const [language, setLanguage] = useState<Language>('classic-asp')
  const [loading, setLoading] = useState(false)
  const [validationError, setValidationError] = useState<string | null>(null)
  const [uploadError, setUploadError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const [result, setResult] = useState<AnalysisResult | null>(null)

  const clearMessages = () => {
    setValidationError(null)
    setUploadError(null)
    setApiError(null)
  }

  const handleFileUpload = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.currentTarget.files?.[0]
    if (!file) return

    clearMessages()
    const detectedLanguage = languageFromFileName(file.name)
    if (detectedLanguage === null) {
      setUploadError('Unsupported file type. Use .asp, .inc, .aspx or .aspx.cs.')
      return
    }

    try {
      setCode(await file.text())
      setFileName(file.name)
      setLanguage(detectedLanguage)
      setResult(null)
    } catch {
      setUploadError('The file could not be read.')
    }
  }

  const handleAnalyze = async () => {
    clearMessages()

    const trimmedCode = code.trim()
    if (!trimmedCode) {
      setValidationError('Enter code or upload a file before analyzing.')
      return
    }

    if (code.length > MAX_CODE_LENGTH) {
      setValidationError('The code is too large (limit: 200,000 characters).')
      return
    }

    setLoading(true)
    setResult(null)
    try {
      setResult(await analyzeCode({ fileName, language, code }))
    } catch {
      // Details are never shown to the user; the API error message is for developers.
      setApiError('Something went wrong while analyzing the code. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="app">
      <h1 data-testid="app-title">LegacyLens</h1>
      <p>
        Analyze Classic ASP and ASP.NET WebForms pages: findings, summary and a
        modern ASP.NET Core migration proposal.
      </p>

      <nav className="nav-bar">
        <button
          type="button"
          data-testid="nav-analyze"
          className={`nav-button ${view === 'analyze' ? 'nav-button-active' : ''}`}
          aria-current={view === 'analyze' ? 'page' : undefined}
          onClick={() => setView('analyze')}
        >
          Analyze
        </button>
        <button
          type="button"
          data-testid="nav-history"
          className={`nav-button ${view === 'history' ? 'nav-button-active' : ''}`}
          aria-current={view === 'history' ? 'page' : undefined}
          onClick={() => setView('history')}
        >
          History
        </button>
      </nav>

      {view === 'history' && <HistoryPage />}

      {view === 'analyze' && (
        <>
          <div className="input-section">
            <div className="form-group">
              <label htmlFor="file-upload">Upload a file:</label>
              <input
                id="file-upload"
                data-testid="file-upload"
                type="file"
                accept=".asp,.inc,.aspx,.cs"
                onChange={handleFileUpload}
              />
            </div>

            <div className="form-group">
              <label htmlFor="code-input">Or paste code:</label>
              <textarea
                id="code-input"
                data-testid="code-input"
                value={code}
                onChange={(e) => {
                  setCode(e.target.value)
                  clearMessages()
                }}
                rows={16}
                placeholder="Paste your Classic ASP or ASP.NET code here..."
              />
            </div>

            <div className="form-group">
              <label htmlFor="language-select">Language:</label>
              <select
                id="language-select"
                data-testid="language-select"
                value={language}
                onChange={(e) => setLanguage(e.target.value as Language)}
              >
                <option value="classic-asp">Classic ASP</option>
                <option value="webforms">WebForms</option>
              </select>
            </div>

            <button
              data-testid="analyze-button"
              onClick={handleAnalyze}
              disabled={loading}
              className="analyze-button"
            >
              {loading ? 'Analyzing...' : 'Analyze'}
            </button>

            {validationError && (
              <div data-testid="validation-error" className="error">
                {validationError}
              </div>
            )}

            {uploadError && (
              <div data-testid="upload-error" className="error">
                {uploadError}
              </div>
            )}

            {apiError && (
              <div data-testid="api-error" className="error">
                {apiError}
              </div>
            )}
          </div>

          {loading && <div data-testid="loading">Analyzing…</div>}

          {!loading && result !== null && <AnalysisView result={result} />}
        </>
      )}
    </main>
  )
}

export default App
