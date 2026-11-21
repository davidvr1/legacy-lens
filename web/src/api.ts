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

export interface AnalysisResult {
  id: string
  fileName: string
  language: Language
  findings: Finding[]
}

export class ApiError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'ApiError'
  }
}

export async function analyzeCode(request: AnalysisRequest): Promise<AnalysisResult> {
  try {
    const response = await fetch('/api/analyses', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
    })

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

    return (await response.json()) as AnalysisResult
  } catch (error) {
    if (error instanceof ApiError) {
      throw error
    }
    throw new ApiError('Network error while analyzing code')
  }
}
