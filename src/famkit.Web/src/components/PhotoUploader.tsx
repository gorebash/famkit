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
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? [])
    if (files.length === 0) return

    setError(null)
    setStatus(files.length === 1
      ? 'Identifying ingredients...'
      : `Identifying ingredients across ${files.length} photos, then merging...`)

    try {
      if (files.length === 1) {
        const result = await api.identifyIngredients(files[0])
        setDetected(result.ingredients)
        setSelected(new Set(result.ingredients.map((i) => i.name)))
      } else {
        const result = await api.identifyIngredientsBatch(files)
        setDetected(result.merged.ingredients)
        setSelected(new Set(result.merged.ingredients.map((i) => i.name)))
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to identify ingredients')
    } finally {
      setStatus(null)
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
        Scan photos
        <input
          ref={inputRef}
          type="file"
          accept="image/*"
          capture="environment"
          multiple
          onChange={handleFileChange}
          hidden
        />
      </label>
      <p className="muted uploader-hint">Pick one or several photos of the same fridge/pantry — the results are merged.</p>
      {status && <p>{status}</p>}
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
