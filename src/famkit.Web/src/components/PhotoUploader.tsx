import { useRef, useState } from 'react'
import { api } from '../api/client'
import type { IdentifiedIngredient } from '../api/types'

interface PhotoUploaderProps {
  onConfirm: (ingredients: IdentifiedIngredient[]) => void
}

export function PhotoUploader({ onConfirm }: PhotoUploaderProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [detected, setDetected] = useState<IdentifiedIngredient[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return

    setLoading(true)
    setError(null)
    try {
      const result = await api.identifyIngredients(file)
      setDetected(result.ingredients)
      setSelected(new Set(result.ingredients.map((i) => i.name)))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to identify ingredients')
    } finally {
      setLoading(false)
      if (inputRef.current) inputRef.current.value = ''
    }
  }

  function toggle(name: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(name)) next.delete(name)
      else next.add(name)
      return next
    })
  }

  function confirm() {
    onConfirm(detected.filter((i) => selected.has(i.name)))
    setDetected([])
    setSelected(new Set())
  }

  return (
    <div className="photo-uploader">
      <label className="button">
        Scan a photo
        <input
          ref={inputRef}
          type="file"
          accept="image/*"
          capture="environment"
          onChange={handleFileChange}
          hidden
        />
      </label>
      {loading && <p>Identifying ingredients...</p>}
      {error && <p className="error">{error}</p>}
      {detected.length > 0 && (
        <div className="detected-ingredients">
          <p>Confirm what to add to your pantry:</p>
          <ul>
            {detected.map((ingredient) => (
              <li key={ingredient.name}>
                <label>
                  <input
                    type="checkbox"
                    checked={selected.has(ingredient.name)}
                    onChange={() => toggle(ingredient.name)}
                  />
                  {ingredient.name}
                  {ingredient.estimatedQuantity ? ` (${ingredient.estimatedQuantity})` : ''}
                </label>
              </li>
            ))}
          </ul>
          <button onClick={confirm} disabled={selected.size === 0}>
            Add {selected.size} item{selected.size === 1 ? '' : 's'} to pantry
          </button>
        </div>
      )}
    </div>
  )
}
