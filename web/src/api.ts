export type Language = 'classic-asp' | 'webforms'
export type Severity = 'Info' | 'Low' | 'Medium' | 'High'

export interface Finding {
  ruleId: string
  severity: Severity
  line: number
  title: string
  explanation: string
  fix: string
}

export interface AnalysisRequest {
  fileName: string
  language: Language
  code: string
}

export interface CodeSummary {
  purpose: string
  inputs: string[]
  outputs: string[]
  databaseAccess: string[]
  includes: string[]
  comObjects: string[]
}

export interface MigrationSource {
  id: string
  title: string
}

export interface CodeDiagnostic {
  id: string
  message: string
  line: number
}

export interface MigrationProposal {
  code: string
  notes: string[]
  sources: MigrationSource[]
  compilesOk: boolean
  diagnostics: CodeDiagnostic[]
  repaired: boolean
}

export interface AnalysisResult {
  id: string
  fileName: string
  language: Language
  createdAt: string
  findings: Finding[]
  summary: CodeSummary | null
  summaryError: string | null
  migration: MigrationProposal | null
  migrationError: string | null
}

export interface FindingCounts {
  high: number
  medium: number
  low: number
  info: number
}

export interface AnalysisListItem {
  id: string
  fileName: string
  language: Language
  createdAt: string
  findingCounts: FindingCounts
}

export class ApiError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'ApiError'
  }
}

async function requestJson<T>(
  url: string,
  init: RequestInit | undefined,
  networkErrorMessage: string,
): Promise<T> {
  try {
    const response = await fetch(url, init)

    if (!response.ok) {
      let errorMessage = 'Unknown error'
      try {
        const data = (await response.json()) as { error?: string }
        errorMessage = data.error || errorMessage
      } catch {
        // If body is not JSON, use a generic message
      }
      throw new ApiError(errorMessage)
    }

    return (await response.json()) as T
  } catch (error) {
    if (error instanceof ApiError) {
      throw error
    }
    throw new ApiError(networkErrorMessage)
  }
}

export function analyzeCode(request: AnalysisRequest): Promise<AnalysisResult> {
  return requestJson<AnalysisResult>(
    '/api/analyses',
    {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
    },
    'Network error while analyzing code',
  )
}

export function listAnalyses(): Promise<AnalysisListItem[]> {
  return requestJson<AnalysisListItem[]>(
    '/api/analyses',
    undefined,
    'Network error while loading the history',
  )
}

export function getAnalysis(id: string): Promise<AnalysisResult> {
  return requestJson<AnalysisResult>(
    `/api/analyses/${encodeURIComponent(id)}`,
    undefined,
    'Network error while loading the analysis',
  )
}
