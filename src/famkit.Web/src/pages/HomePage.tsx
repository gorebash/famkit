import { useEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { ChatMessage, IdentifiedIngredient, PantryItem } from '../api/types'
import { PhotoUploader } from '../components/PhotoUploader'

const SUGGESTED_PROMPTS = [
  "What's in my fridge?",
  'Do we have any eggs?',
  "I'm making tacos for dinner — what do I need from the store?",
  'Add milk to the fridge',
]

export function HomePage() {
  return (
    <div className="home-page">
      <section className="home-chat-section">
        <h2>Ask FamKit</h2>
        <ChatPanel />
      </section>

      <section className="home-pantry-section">
        <h2>Pantry &amp; Fridge</h2>
        <PantryPanel />
      </section>
    </div>
  )
}

// -------- Chat --------

function ChatPanel() {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const transcriptRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
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

  return (
    <div className="chat-panel">
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

// -------- Pantry --------

function PantryPanel() {
  const [items, setItems] = useState<PantryItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [name, setName] = useState('')
  const [category, setCategory] = useState<'fridge' | 'pantry'>('fridge')

  async function refresh() {
    setLoading(true)
    try {
      setItems(await api.getPantry())
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load pantry')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    refresh()
  }, [])

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim()) return
    await api.addPantryItem({ name: name.trim(), category })
    setName('')
    refresh()
  }

  async function handleDelete(id: string) {
    await api.deletePantryItem(id)
    refresh()
  }

  async function handlePhotoConfirm(ingredients: IdentifiedIngredient[]) {
    await Promise.all(
      ingredients.map((i) =>
        api.addPantryItem({
          name: i.name,
          category: i.category === 'pantry' ? 'pantry' : 'fridge',
          quantity: i.estimatedQuantity ?? undefined,
          source: 'photo',
        }),
      ),
    )
    refresh()
  }

  return (
    <div>
      <PhotoUploader onConfirm={handlePhotoConfirm} />

      <form onSubmit={handleAdd} className="inline-form">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Add an item, e.g. eggs"
        />
        <select value={category} onChange={(e) => setCategory(e.target.value as 'fridge' | 'pantry')}>
          <option value="fridge">Fridge</option>
          <option value="pantry">Pantry</option>
        </select>
        <button type="submit">Add</button>
      </form>

      {error && <p className="error">{error}</p>}
      {loading ? (
        <p>Loading...</p>
      ) : items.length === 0 ? (
        <p className="muted">Nothing in your pantry yet — scan a photo or add items manually.</p>
      ) : (
        <ul className="pantry-list">
          {items.map((item) => (
            <li key={item.rowKey}>
              <span className="pantry-item-name">{item.name}</span>
              <span className="pantry-item-meta">
                {item.category}
                {item.quantity ? ` · ${item.quantity}` : ''}
              </span>
              <button className="link" onClick={() => handleDelete(item.rowKey)}>
                Remove
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
