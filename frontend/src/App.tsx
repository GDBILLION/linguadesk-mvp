import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  ArrowDownLeft, ArrowLeft, ArrowRight, ArrowUpRight, Bell, Check, CheckCheck,
  ChevronDown, CircleHelp, Clock3, Command, Filter, Globe2, Inbox, Languages,
  LifeBuoy, LoaderCircle, MoreHorizontal, PanelLeftClose, Plus, Search, Send,
  Settings2, ShieldCheck, Sparkles, WandSparkles, X, Zap
} from 'lucide-react'
import { api } from './api'
import type { Conversation, DashboardStats, LanguageCode, Message } from './types'

const languageMeta: Record<LanguageCode, { name: string; native: string; flag: string }> = {
  en: { name: 'English', native: 'English', flag: '🇬🇧' },
  es: { name: 'Spanish', native: 'Español', flag: '🇪🇸' },
  fr: { name: 'French', native: 'Français', flag: '🇫🇷' },
  'zh-CN': { name: 'Mandarin Chinese', native: '简体中文', flag: '🇨🇳' },
  de: { name: 'German', native: 'Deutsch', flag: '🇩🇪' },
}

const initials = (name: string) => name.split(' ').map(x => x[0]).slice(0, 2).join('').toUpperCase()
const timeAgo = (date: string) => {
  const mins = Math.max(0, Math.floor((Date.now() - new Date(date).getTime()) / 60000))
  if (mins < 1) return 'now'
  if (mins < 60) return `${mins}m`
  if (mins < 1440) return `${Math.floor(mins / 60)}h`
  return `${Math.floor(mins / 1440)}d`
}

export default function App() {
  const [stats, setStats] = useState<DashboardStats | null>(null)
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [activeId, setActiveId] = useState<string | null>(null)
  const [messages, setMessages] = useState<Message[]>([])
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('all')
  const [languageFilter, setLanguageFilter] = useState('all')
  const [draft, setDraft] = useState('')
  const [preview, setPreview] = useState<{ text: string; isDemo: boolean } | null>(null)
  const [loading, setLoading] = useState(true)
  const [messagesLoading, setMessagesLoading] = useState(false)
  const [translating, setTranslating] = useState(false)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState('')
  const [showFilters, setShowFilters] = useState(false)
  const [mobileThread, setMobileThread] = useState(false)

  const active = conversations.find(c => c.id === activeId) ?? null

  const loadConversations = useCallback(async () => {
    try {
      const result = await api.conversations({
        status: statusFilter === 'all' ? undefined : statusFilter,
        language: languageFilter === 'all' ? undefined : languageFilter,
        search: search.trim() || undefined,
      })
      setConversations(result)
      setActiveId(current => current && result.some(c => c.id === current) ? current : result[0]?.id ?? null)
      setError('')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unable to load conversations')
    }
  }, [statusFilter, languageFilter, search])

  const loadStats = useCallback(async () => {
    try { setStats(await api.dashboard()) } catch { /* API status shown in workspace badge */ }
  }, [])

  useEffect(() => {
    let cancelled = false
    Promise.all([api.dashboard(), api.conversations()]).then(([s, c]) => {
      if (cancelled) return
      setStats(s)
      setConversations(c)
      setActiveId(c[0]?.id ?? null)
      setLoading(false)
    }).catch(e => {
      if (!cancelled) {
        setError(e instanceof Error ? e.message : 'Could not connect to API')
        setLoading(false)
      }
    })
    return () => { cancelled = true }
  }, [])

  useEffect(() => { loadConversations() }, [loadConversations])

  useEffect(() => {
    if (!activeId) { setMessages([]); return }
    let cancelled = false
    setMessagesLoading(true)
    api.messages(activeId).then(m => { if (!cancelled) setMessages(m) })
      .catch(e => { if (!cancelled) setError(e instanceof Error ? e.message : 'Could not load messages') })
      .finally(() => { if (!cancelled) setMessagesLoading(false) })
    setDraft('')
    setPreview(null)
    return () => { cancelled = true }
  }, [activeId])

  const activeLanguage = active?.customerLanguage ?? 'en'
  const filteredCount = conversations.length
  const usagePercent = stats ? Math.min(100, Math.round(stats.monthlyUsage / Math.max(1, stats.monthlyLimit) * 100)) : 0

  const refreshActive = async () => {
    if (!activeId) return
    const [m] = await Promise.all([api.messages(activeId), loadConversations(), loadStats()])
    setMessages(m)
  }

  const chooseConversation = (id: string) => {
    setActiveId(id)
    setMobileThread(true)
  }

  const createPreview = async () => {
    if (!draft.trim() || !active) return
    setTranslating(true)
    setError('')
    try {
      const result = await api.preview(draft, 'en', active.customerLanguage)
      setPreview({ text: result.translatedText, isDemo: result.isDemo })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Translation preview failed')
    } finally { setTranslating(false) }
  }

  const sendReply = async () => {
    if (!active || !draft.trim()) return
    if (!preview || preview.isDemo) {
      setError('Configure a real translation provider before sending customer-facing translations. Demo translations are not safe to send.')
      return
    }
    setSending(true)
    setError('')
    try {
      await api.sendReply(active.id, draft.trim(), preview.text)
      setDraft('')
      setPreview(null)
      await refreshActive()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not send reply')
    } finally { setSending(false) }
  }

  const changeStatus = async (status: string) => {
    if (!active) return
    try {
      await api.updateStatus(active.id, status)
      await loadConversations()
      await loadStats()
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not update status') }
  }

  const latest = useMemo(() => [...messages].reverse().find(m => m.senderType === 'customer'), [messages])

  return (
    <div className="app-shell">
      <aside className="rail">
        <div className="brand-mark"><Languages size={21} strokeWidth={2.2} /></div>
        <div className="rail-divider" />
        <button className="rail-item active" title="Inbox"><Inbox size={19} /><span className="rail-dot" /></button>
        <button className="rail-item" title="Translation quality"><Sparkles size={19} /></button>
        <button className="rail-item" title="Languages"><Globe2 size={19} /></button>
        <div className="rail-spacer" />
        <button className="rail-item" title="Help"><CircleHelp size={19} /></button>
        <button className="rail-item" title="Settings"><Settings2 size={19} /></button>
        <div className="profile-mini">AM</div>
      </aside>

      <aside className={`inbox-sidebar ${mobileThread ? 'mobile-hidden' : ''}`}>
        <div className="workspace-switcher">
          <div className="workspace-symbol">N</div>
          <div className="workspace-copy"><strong>{stats?.workspace ?? 'Northstar Commerce'}</strong><span>Support workspace</span></div>
          <ChevronDown size={15} className="muted" />
        </div>
        <div className="sidebar-heading">
          <div><div className="eyebrow">WORKSPACE</div><h1>Inbox <span className="count-pill">{filteredCount}</span></h1></div>
          <button className="icon-button" title="New conversation"><Plus size={18} /></button>
        </div>
        <div className="search-box"><Search size={16} /><input value={search} onChange={setSearch} placeholder="Search conversations..." /><kbd>⌘ K</kbd></div>
        <div className="filter-row">
          <button className={`filter-chip ${statusFilter === 'all' ? 'selected' : ''}`} onClick={() => setStatusFilter('all')}>All</button>
          <button className={`filter-chip ${statusFilter === 'open' ? 'selected' : ''}`} onClick={() => setStatusFilter('open')}>Open</button>
          <button className={`filter-chip ${statusFilter === 'pending' ? 'selected' : ''}`} onClick={() => setStatusFilter('pending')}>Pending</button>
          <button className={`filter-icon ${showFilters ? 'selected' : ''}`} onClick={() => setShowFilters(!showFilters)} title="More filters"><Filter size={15} /></button>
        </div>
        {showFilters && <div className="filter-extra"><label>Language<select value={languageFilter} onChange={e => setLanguageFilter(e.target.value)}><option value="all">All languages</option>{Object.entries(languageMeta).map(([code, meta]) => <option key={code} value={code}>{meta.name}</option>)}</select></label><label>Status<select value={statusFilter} onChange={e => setStatusFilter(e.target.value)}><option value="all">All</option><option value="open">Open</option><option value="pending">Pending</option><option value="resolved">Resolved</option></select></label></div>}
        <div className="list-caption"><span>YOUR CONVERSATIONS</span><span><ArrowDownLeft size={13} /> Recent</span></div>
        <div className="conversation-list">
          {loading && <div className="empty-state"><LoaderCircle className="spin" size={22} />Loading inbox</div>}
          {!loading && conversations.map(c => {
            const selected = c.id === activeId
            return <button key={c.id} className={`conversation-card ${selected ? 'selected' : ''}`} onClick={() => chooseConversation(c.id)}>
              <div className={`avatar avatar-${c.customerLanguage}`}>{initials(c.customer)}</div>
              <div className="conversation-content">
                <div className="conversation-top"><strong>{c.customer}</strong><time>{timeAgo(c.updatedAt)}</time></div>
                <div className="subject-line">{c.subject}</div>
                <p>{c.lastMessage || 'No messages yet'}</p>
                <div className="conversation-bottom"><span className="language-tag">{languageMeta[c.customerLanguage]?.flag} {languageMeta[c.customerLanguage]?.name}</span><span className={`status-dot status-${c.status}`}>{c.status}</span></div>
              </div>
            </button>
          })}
          {!loading && conversations.length === 0 && <div className="empty-state">No conversations match these filters.</div>}
        </div>
        <div className="sidebar-footer">
          <div className="usage-title"><span>MONTHLY CONVERSATIONS</span><strong>{stats?.monthlyUsage ?? 0}<span> / {stats?.monthlyLimit ?? 1000}</span></strong></div>
          <div className="usage-track"><div style={{ width: `${usagePercent}%` }} /></div>
          <div className="usage-foot"><span>{100 - usagePercent}% remaining</span><button>Manage plan <ArrowUpRight size={12} /></button></div>
        </div>
      </aside>

      <main className={`main-panel ${mobileThread ? 'mobile-visible' : ''}`}>
        <header className="topbar">
          <div className="topbar-left">
            <button className="mobile-back" onClick={() => setMobileThread(false)}><ArrowLeft size={18} /></button>
            <div><div className="eyebrow">CUSTOMER SUPPORT <span className="tiny-separator">/</span> INBOX</div><div className="topbar-title">Conversations <span className="live-status"><i /> LIVE</span></div></div>
          </div>
          <div className="topbar-actions"><div className="connection-status"><span /> All systems operational</div><button className="icon-button" title="Notifications"><Bell size={17} /><i className="notification-dot" /></button><div className="top-avatar">AM</div></div>
        </header>

        {error && <div className="error-banner"><X size={16} /><span>{error}</span><button onClick={() => setError('')}><X size={14} /></button></div>}

        {!active && !loading ? <div className="empty-main"><Inbox size={34} /><h2>Your inbox is clear</h2><p>New customer conversations will appear here.</p></div> : active && <>
          <section className="conversation-header">
            <div className="customer-title">
              <div className={`avatar avatar-large avatar-${active.customerLanguage}`}>{initials(active.customer)}</div>
              <div><div className="customer-name">{active.customer}<span className="verified-mark"><Check size={11} /></span></div><div className="customer-subtitle">{active.subject} <span>·</span> Customer since {new Date(active.createdAt).toLocaleDateString(undefined, { month: 'short', year: 'numeric' })}</div></div>
            </div>
            <div className="header-controls"><div className="language-pair"><span>{languageMeta[active.customerLanguage]?.flag}</span><ArrowRight size={12} /><span>🇬🇧</span><div><strong>{languageMeta[active.customerLanguage]?.name}</strong><small>→ English</small></div></div><button className="outline-button" onClick={() => changeStatus(active.status === 'resolved' ? 'open' : 'resolved')}>{active.status === 'resolved' ? 'Reopen' : 'Resolve'} <CheckCheck size={15} /></button><button className="icon-button" title="More options"><MoreHorizontal size={18} /></button></div>
          </section>

          <section className="translation-banner"><div className="translation-icon"><WandSparkles size={16} /></div><div><strong>Language bridge active</strong><p>Messages are translated from {languageMeta[active.customerLanguage]?.name} to English, and your replies back again.</p></div><div className="banner-badge"><span /> AUTO-TRANSLATE</div></section>

          <section className="thread" key={active.id}>
            <div className="thread-date"><span />TODAY<span /></div>
            {messagesLoading && <div className="empty-state"><LoaderCircle className="spin" size={20} />Loading messages…</div>}
            {!messagesLoading && messages.map(message => {
              const isCustomer = message.senderType === 'customer'
              return <article key={message.id} className={`message-row ${isCustomer ? 'from-customer' : 'from-agent'}`}>
                {isCustomer && <div className={`avatar message-avatar avatar-${active.customerLanguage}`}>{initials(active.customer)}</div>}
                <div className="message-column">
                  <div className="message-meta"><strong>{isCustomer ? active.customer : message.senderName}</strong><time>{new Date(message.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</time>{!isCustomer && <span className="agent-label">SUPPORT</span>}</div>
                  <div className={`message-bubble ${isCustomer ? 'customer-bubble' : 'agent-bubble'}`}>
                    <p className={isCustomer ? 'original-message' : ''}>{message.originalText}</p>
                    {isCustomer && message.translatedText && message.originalLanguage !== 'en' && <div className="translated-block"><div className="translated-label"><Languages size={12} /> <span>TRANSLATED TO ENGLISH</span><Check size={12} /></div><p>{message.translatedText}</p></div>}
                    {!isCustomer && message.translatedText && message.targetLanguage !== 'en' && <div className="translated-block outgoing-translation"><div className="translated-label"><Languages size={12} /><span>DELIVERED IN {languageMeta[message.targetLanguage]?.name.toUpperCase()}</span><Check size={12} /></div><p>{message.translatedText}</p></div>}
                  </div>
                  <div className="message-foot"><span><ShieldCheck size={12} /> {isCustomer ? 'Original preserved' : 'Translation reviewed'}</span>{!isCustomer && <span><CheckCheck size={13} /> Sent</span>}</div>
                </div>
                {!isCustomer && <div className="agent-avatar">AM</div>}
              </article>
            })}
            {!messagesLoading && messages.length === 0 && <div className="empty-state">No messages yet. Send a reply to get started.</div>}
            <div className="thread-bottom-space" />
          </section>

          <section className="composer-area">
            {preview && <div className={`translation-preview ${preview.isDemo ? 'demo-preview' : ''}`}><div className="preview-head"><span><Sparkles size={14} /> TRANSLATION PREVIEW {preview.isDemo && '· DEMO ONLY'}</span><button onClick={() => setPreview(null)}><X size={15} /></button></div><p>{preview.text}</p>{preview.isDemo && <small>Demo output is not a real translation and cannot be sent.</small>}</div>}
            <div className="composer">
              <div className="composer-toolbar"><div className="compose-language"><span>🇬🇧</span><span>English reply</span><ArrowRight size={12} /><span>{languageMeta[active.customerLanguage]?.flag} {languageMeta[active.customerLanguage]?.native}</span></div><span className="secure-note"><ShieldCheck size={12} /> Private workspace</span></div>
              <textarea value={draft} onChange={e => { setDraft(e.target.value); setPreview(null) }} onKeyDown={e => { if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) { e.preventDefault(); createPreview() } }} placeholder="Write your reply in English…" rows={3} />
              <div className="composer-bottom"><div className="composer-hints"><span><Command size={12} /> + Enter to preview</span><span className="dot-sep">·</span><span>{draft.length} characters</span></div><div className="composer-actions"><button className="preview-button" onClick={createPreview} disabled={!draft.trim() || translating}>{translating ? <LoaderCircle size={15} className="spin" /> : <WandSparkles size={15} />} Preview translation</button><button className="send-button" onClick={sendReply} disabled={!preview || preview.isDemo || sending}>{sending ? <LoaderCircle size={15} className="spin" /> : <Send size={15} />} Send reply</button></div></div>
            </div>
            <div className="composer-disclaimer"><ShieldCheck size={12} /> AI translation can make mistakes. Review before sending.</div>
          </section>
        </>}
      </main>

      <aside className="context-panel">
        <div className="context-head"><div><div className="eyebrow">CONVERSATION</div><h3>Details</h3></div><button className="icon-button" title="Collapse panel"><PanelLeftClose size={17} /></button></div>
        {active ? <>
          <div className="context-customer"><div className={`avatar avatar-xl avatar-${active.customerLanguage}`}>{initials(active.customer)}</div><h3>{active.customer}</h3><p>{active.customer === 'Sofia Martinez' ? 'sofia@example.com' : active.customer === 'Camille Laurent' ? 'camille@example.com' : active.customer === 'Lukas Weber' ? 'lukas@example.com' : active.customer === 'Wei Chen' ? 'wei@example.com' : 'emma@example.com'}</p><div className="customer-tags"><span className="tag-muted">Customer</span><span className="tag-green"><i /> Active</span></div></div>
          <div className="context-section"><div className="section-title">LANGUAGE PROFILE</div><div className="language-profile"><div className="language-profile-icon"><Globe2 size={16} /></div><div><strong>{languageMeta[active.customerLanguage]?.name}</strong><span>Customer's preferred language</span></div><span className="language-flag">{languageMeta[active.customerLanguage]?.flag}</span></div><div className="language-profile"><div className="language-profile-icon english-icon">EN</div><div><strong>English</strong><span>Agent's working language</span></div><span className="language-flag">🇬🇧</span></div></div>
          <div className="context-section"><div className="section-title">CONVERSATION STATUS</div><div className="status-select-wrap"><span className={`status-dot status-${active.status}`}>{active.status}</span><ChevronDown size={14} /><select value={active.status} onChange={e => changeStatus(e.target.value)} aria-label="Conversation status"><option value="open">Open</option><option value="pending">Pending</option><option value="resolved">Resolved</option></select></div><div className="detail-row"><span>Assigned to</span><strong><span className="tiny-avatar">AM</span>{active.assignedAgent}</strong></div><div className="detail-row"><span>Conversation ID</span><button className="copy-id" onClick={() => navigator.clipboard?.writeText(active.id)}>{active.id.slice(0, 8)}…</button></div><div className="detail-row"><span>Last activity</span><strong>{timeAgo(active.updatedAt)} ago</strong></div></div>
          <div className="context-section"><div className="section-title">TRANSLATION ACTIVITY</div><div className="activity-item"><div className="activity-icon"><ArrowDownLeft size={14} /></div><div><strong>Incoming translation</strong><p>{languageMeta[active.customerLanguage]?.name} → English</p></div><span className="activity-ok"><Check size={12} /></span></div><div className="activity-item"><div className="activity-icon outbound"><ArrowUpRight size={14} /></div><div><strong>Reply translation</strong><p>English → {languageMeta[active.customerLanguage]?.name}</p></div><span className="activity-ok"><Check size={12} /></span></div></div>
          <div className="context-bottom-card"><div className="sparkle-orb"><Sparkles size={16} /></div><div><strong>Lost in translation?</strong><p>Every message keeps its original meaning alongside the translation.</p><button onClick={() => setError('Translation quality insights are planned for a future release.')}>Explore quality insights <ArrowRight size={13} /></button></div></div>
        </> : <div className="empty-state">Select a conversation to view details.</div>}
      </aside>
    </div>
  )
}
