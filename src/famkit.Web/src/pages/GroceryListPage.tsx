import { useEffect, useState } from 'react'
import { api } from '../api/client'

export function GroceryListPage() {
  const [groceryList, setGroceryList] = useState<string[]>([])
  const [checked, setChecked] = useState<Set<string>>(new Set())
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const recipes = await api.getRecipes()
        const diffs = await Promise.all(
          recipes.map((r) => api.evaluateRecipe({ recipeId: r.id })),
        )
        const merged = Array.from(
          new Set(diffs.flatMap((d) => d.missing).map((i) => i.toLowerCase())),
        ).sort()
        setGroceryList(merged)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to build grocery list')
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [])

  function toggle(item: string) {
    setChecked((prev) => {
      const next = new Set(prev)
      if (next.has(item)) next.delete(item)
      else next.add(item)
      return next
    })
  }

  return (
    <div>
      <h1>Grocery List</h1>
      <p className="muted">Missing ingredients across all your saved recipes, compared to your current pantry.</p>

      {error && <p className="error">{error}</p>}
      {loading ? (
        <p>Loading...</p>
      ) : groceryList.length === 0 ? (
        <p className="muted">Nothing needed — add some recipes on the Recipes page.</p>
      ) : (
        <ul className="grocery-list">
          {groceryList.map((item) => (
            <li key={item}>
              <label className={checked.has(item) ? 'checked' : ''}>
                <input type="checkbox" checked={checked.has(item)} onChange={() => toggle(item)} />
                {item}
              </label>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
