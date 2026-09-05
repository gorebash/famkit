import { useState } from 'react'
import { api } from '../api/client'
import type { MealPlanResponse, MealSuggestion } from '../api/types'
import { RecipeCard } from '../components/RecipeCard'
import { IngredientList } from '../components/IngredientList'

type SaveStatus = { state: 'saving' | 'saved' | 'error'; error?: string }

export function MealIdeasPage() {
  const [result, setResult] = useState<MealPlanResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saveStatus, setSaveStatus] = useState<Record<number, SaveStatus>>({})

  async function suggest() {
    setLoading(true)
    setError(null)
    setSaveStatus({})
    try {
      setResult(await api.suggestMealPlan())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to get meal suggestions')
    } finally {
      setLoading(false)
    }
  }

  async function handleSave(suggestion: MealSuggestion) {
    setSaveStatus((prev) => ({ ...prev, [suggestion.spoonacularId]: { state: 'saving' } }))
    try {
      await api.saveRecipeFromSpoonacular({ spoonacularId: suggestion.spoonacularId })
      setSaveStatus((prev) => ({ ...prev, [suggestion.spoonacularId]: { state: 'saved' } }))
    } catch (err) {
      setSaveStatus((prev) => ({
        ...prev,
        [suggestion.spoonacularId]: {
          state: 'error',
          error: err instanceof Error ? err.message : 'Save failed',
        },
      }))
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
            {result.suggestions.map((s) => {
              const status = saveStatus[s.spoonacularId]
              return (
                <RecipeCard
                  key={s.spoonacularId}
                  suggestion={s}
                  onSave={handleSave}
                  saveState={status?.state ?? 'idle'}
                  saveError={status?.error}
                />
              )
            })}
          </div>

          <h2>Combined grocery list</h2>
          <IngredientList items={result.groceryList} emptyLabel="You have everything you need!" />
        </>
      )}
    </div>
  )
}
