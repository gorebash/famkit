import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { IngredientDiffResult, Recipe } from '../api/types'
import { IngredientList } from '../components/IngredientList'

export function AddRecipePage() {
  const [recipes, setRecipes] = useState<Recipe[]>([])
  const [title, setTitle] = useState('')
  const [ingredientsText, setIngredientsText] = useState('')
  const [instructions, setInstructions] = useState('')
  const [diffByRecipe, setDiffByRecipe] = useState<Record<string, IngredientDiffResult>>({})
  const [error, setError] = useState<string | null>(null)

  async function refresh() {
    setRecipes(await api.getRecipes())
  }

  useEffect(() => {
    refresh()
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const lines = ingredientsText
      .split('\n')
      .map((l) => l.trim())
      .filter(Boolean)
    if (!title.trim() || lines.length === 0) return

    setError(null)
    try {
      const recipe = await api.addRecipe({
        title: title.trim(),
        ingredients: lines.map((name) => ({ name })),
        instructions: instructions.trim() || undefined,
      })
      setTitle('')
      setIngredientsText('')
      setInstructions('')
      await refresh()
      await checkAgainstPantry(recipe.id)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add recipe')
    }
  }

  async function checkAgainstPantry(recipeId: string) {
    const diff = await api.evaluateRecipe({ recipeId })
    setDiffByRecipe((prev) => ({ ...prev, [recipeId]: diff }))
  }

  async function handleDelete(id: string) {
    await api.deleteRecipe(id)
    refresh()
  }

  return (
    <div>
      <h1>Recipes</h1>

      <form onSubmit={handleSubmit} className="stacked-form">
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Recipe title"
        />
        <textarea
          value={ingredientsText}
          onChange={(e) => setIngredientsText(e.target.value)}
          placeholder={'Ingredients, one per line, e.g.\nflour\n2 eggs\nmilk'}
          rows={5}
        />
        <textarea
          value={instructions}
          onChange={(e) => setInstructions(e.target.value)}
          placeholder="Instructions (optional)"
          rows={3}
        />
        <button type="submit">Save recipe &amp; check pantry</button>
      </form>

      {error && <p className="error">{error}</p>}

      <ul className="recipe-list">
        {recipes.map((recipe) => {
          const diff = diffByRecipe[recipe.id]
          return (
            <li key={recipe.id} className="card">
              <h3>{recipe.title}</h3>
              <IngredientList items={recipe.ingredients.map((i) => i.name)} />
              <div className="recipe-actions">
                <button className="link" onClick={() => checkAgainstPantry(recipe.id)}>
                  Check against pantry
                </button>
                <button className="link" onClick={() => handleDelete(recipe.id)}>
                  Delete
                </button>
              </div>
              {diff && (
                <div className="recipe-card-columns">
                  <div>
                    <h4>You have</h4>
                    <IngredientList items={diff.have} />
                  </div>
                  <div>
                    <h4>Missing</h4>
                    <IngredientList items={diff.missing} emptyLabel="Nothing missing!" />
                  </div>
                </div>
              )}
            </li>
          )
        })}
      </ul>
    </div>
  )
}
