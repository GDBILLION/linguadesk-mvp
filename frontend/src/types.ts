export type LanguageCode = 'en' | 'es' | 'fr' | 'zh-CN' | 'de'
export type ConversationStatus = 'open' | 'pending' | 'resolved'

export interface Conversation {
  id: string
  subject: string
  customerLanguage: LanguageCode
  status: ConversationStatus
  assignedAgent: string
  createdAt: string
  updatedAt: string
  customer: string
  lastMessage?: string | null
}

export interface Message {
  id: string
  senderType: 'customer' | 'agent'
  senderName: string
  originalText: string
  originalLanguage: LanguageCode
  translatedText?: string | null
  targetLanguage: LanguageCode
  translationStatus: string
  deliveryStatus: string
  createdAt: string
  isDemoTranslation?: boolean
}

export interface DashboardStats {
  workspace: string
  openConversations: number
  pendingConversations: number
  resolvedConversations: number
  monthlyUsage: number
  monthlyLimit: number
  languages: LanguageCode[]
}
