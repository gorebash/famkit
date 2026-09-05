import { useState } from 'react'
import { api } from '../api/client'
import type { MealPlanResponse } from '../api/types'
import { RecipeCard } from '../components/RecipeCard'
import { IngredientList } from '../components/IngredientList'

export function MealIdeasPage() {
  const [result, setResult] = useState<MealPlanResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function suggest() {
    setLoading(true)
    setError(null)
    try {
      setResult(await api.suggestMealPlan())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to get meal suggestions')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div>
      <h1>What can I make?</h1>
      <button onClick={suggest} disabled={loading}>
        {loading ? 'Thinking...' : 'Suggest meals from my pantry'}
      </button>

      {error && <p className="error">{error}</p>}

      {result && (
        <>
          <div className="recipe-grid">
            {result.suggestions.map((s) => (
              <RecipeCard key={s.spoonacularId} suggestion={s} />
            ))}
          </div>

          <h2>Combined grocery list</h2>
          <IngredientList items={result.groceryList} emptyLabel="You have everything you need!" />
        </>
      )}
    </div>
  )
}
