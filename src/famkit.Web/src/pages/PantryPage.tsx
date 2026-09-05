import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { IdentifiedIngredient, PantryItem } from '../api/types'
import { PhotoUploader } from '../components/PhotoUploader'

export function PantryPage() {
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
      <h1>Pantry &amp; Fridge</h1>
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
