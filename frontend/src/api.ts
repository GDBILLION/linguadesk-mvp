import type { Conversation, DashboardStats, Message, LanguageCode } from './types'

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}/api/v1${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(init?.headers || {}) },
  })
  if (!response.ok) {
    const body = await response.json().catch(() => ({}))
    throw new Error(body.error || body.title || `Request failed (${response.status})`)
  }
  return response.json() as Promise<T>
}

export const api = {
  dashboard: () => request<DashboardStats>('/dashboard'),
  conversations: (filters: { status?: string; language?: string; search?: string } = {}) => {
    const params = new URLSearchParams()
    Object.entries(filters).forEach(([key, value]) => { if (value) params.set(key, value) })
    return request<Conversation[]>(`/conversations?${params.toString()}`)
  },
  messages: (id: string) => request<Message[]>(`/conversations/${id}/messages`),
  preview: (text: string, sourceLanguage: LanguageCode, targetLanguage: LanguageCode) =>
    request<{ translatedText: string; provider: string; isDemo: boolean }>('/translations/preview', {
      method: 'POST',
      body: JSON.stringify({ text, sourceLanguage, targetLanguage }),
    }),
  sendReply: (id: string, text: string, approvedTranslation: string) =>
    request<Message & { provider?: string; isDemoTranslation?: boolean }>(`/conversations/${id}/messages`, {
      method: 'POST', body: JSON.stringify({ text, approvedTranslation }),
    }),
  updateStatus: (id: string, status: string) =>
    request<{ id: string; status: string }>(`/conversations/${id}/status`, {
      method: 'PATCH', body: JSON.stringify({ status }),
    }),
  usage: () => request<{ plan: string; used: number; limit: number }>('/billing/usage'),
}
