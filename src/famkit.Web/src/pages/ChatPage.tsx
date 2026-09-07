import { useEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { ChatMessage } from '../api/types'

const SUGGESTED_PROMPTS = [
  "What's in my fridge?",
  'Do we have any eggs?',
  "I'm making tacos for dinner — what do I need from the store?",
  'Add milk to the fridge',
  'Show me quick breakfast recipes',
]

export function ChatPage() {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const transcriptRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    // Auto-scroll to bottom on new messages.
    transcriptRef.current?.scrollTo({ top: transcriptRef.current.scrollHeight, behavior: 'smooth' })
  }, [messages, loading])

  async function send(promptText?: string) {
    const text = (promptText ?? input).trim()
    if (!text || loading) return

    const userMsg: ChatMessage = { role: 'user', content: text }
    const nextHistory = [...messages, userMsg]
    setMessages(nextHistory)
    setInput('')
    setLoading(true)
    setError(null)

    try {
      const res = await api.chat(nextHistory)
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', content: res.reply, toolActivity: res.toolActivity },
      ])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Chat failed')
    } finally {
      setLoading(false)
    }
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      send()
    }
  }

  function reset() {
    setMessages([])
    setError(null)
  }

  return (
    <div className="chat-page">
      <div className="chat-header">
        <h1>Ask FamKit</h1>
        {messages.length > 0 && (
          <button className="link" onClick={reset}>
            Start over
          </button>
        )}
      </div>

      <div ref={transcriptRef} className="chat-transcript">
        {messages.length === 0 ? (
          <div className="chat-welcome">
            <p className="muted">
              I can see your pantry, add items for you, and check what you need for a recipe. Try:
            </p>
            <div className="chat-suggestions">
              {SUGGESTED_PROMPTS.map((p) => (
                <button key={p} className="chat-suggestion" onClick={() => send(p)}>
                  {p}
                </button>
              ))}
            </div>
          </div>
        ) : (
          messages.map((m, i) => (
            <div key={i} className={`chat-bubble chat-bubble-${m.role}`}>
              {m.toolActivity && m.toolActivity.length > 0 && (
                <div className="chat-tool-activity">
                  {m.toolActivity.map((line, j) => (
                    <div key={j}>🔧 {line}</div>
                  ))}
                </div>
              )}
              <div className="chat-content">{m.content}</div>
            </div>
          ))
        )}
        {loading && <div className="chat-bubble chat-bubble-assistant chat-thinking">Thinking...</div>}
      </div>

      {error && <p className="error">{error}</p>}

      <div className="chat-input-row">
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Ask about your pantry, recipes, or what to buy..."
          rows={2}
          disabled={loading}
        />
        <button onClick={() => send()} disabled={loading || !input.trim()}>
          Send
        </button>
      </div>
    </div>
  )
}
