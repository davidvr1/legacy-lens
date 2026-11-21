import type { Language } from './api'

export const MAX_CODE_LENGTH = 200_000
export const ACCEPTED_EXTENSIONS = ['.asp', '.inc', '.aspx', '.aspx.cs']

export function languageFromFileName(name: string): Language | null {
  const lowerName = name.toLowerCase()

  // Check .aspx.cs first (must come before .cs-like endings)
  if (lowerName.endsWith('.aspx.cs')) {
    return 'webforms'
  }

  // Check other extensions
  if (lowerName.endsWith('.aspx')) {
    return 'webforms'
  }

  if (lowerName.endsWith('.asp') || lowerName.endsWith('.inc')) {
    return 'classic-asp'
  }

  return null
}
