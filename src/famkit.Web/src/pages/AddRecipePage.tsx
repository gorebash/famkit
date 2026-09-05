import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/client'
import type { IngredientDiffResult, MealType, PrepTime, Recipe } from '../api/types'
import { IngredientList } from '../components/IngredientList'

const MEAL_TYPE_LABELS: Record<MealType, string> = {
  breakfast: 'Breakfast',
  lunch: 'Lunch',
  dinner: 'Dinner',
  any: 'Any',
}

const PREP_TIME_LABELS: Record<PrepTime, string> = {
  quick: 'Fast & easy',
  standard: 'Longer',
}

type MealFilter = MealType | 'all'
type PrepFilter = PrepTime | 'all'

export function AddRecipePage() {
  const [recipes, setRecipes] = useState<Recipe[]>([])
  const [title, setTitle] = useState('')
  const [ingredientsText, setIngredientsText] = useState('')
  const [instructions, setInstructions] = useState('')
  const [formMealType, setFormMealType] = useState<MealType>('any')
  const [formPrepTime, setFormPrepTime] = useState<PrepTime>('standard')
  const [diffByRecipe, setDiffByRecipe] = useState<Record<string, IngredientDiffResult>>({})
  const [error, setError] = useState<string | null>(null)

  const [mealFilter, setMealFilter] = useState<MealFilter>('all')
  const [prepFilter, setPrepFilter] = useState<PrepFilter>('all')

  const filteredRecipes = useMemo(() => {
    return recipes.filter((r) => {
      if (mealFilter !== 'all' && r.mealType !== mealFilter) return false
      if (prepFilter !== 'all' && r.prepTime !== prepFilter) return false
      return true
    })
  }, [recipes, mealFilter, prepFilter])

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
        mealType: formMealType,
        prepTime: formPrepTime,
      })
      setTitle('')
      setIngredientsText('')
      setInstructions('')
      setFormMealType('any')
      setFormPrepTime('standard')
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
        <div className="form-row">
          <label>
            Meal:
            <select value={formMealType} onChange={(e) => setFormMealType(e.target.value as MealType)}>
              {(Object.keys(MEAL_TYPE_LABELS) as MealType[]).map((v) => (
                <option key={v} value={v}>{MEAL_TYPE_LABELS[v]}</option>
              ))}
            </select>
          </label>
          <label>
            Prep:
            <select value={formPrepTime} onChange={(e) => setFormPrepTime(e.target.value as PrepTime)}>
              {(Object.keys(PREP_TIME_LABELS) as PrepTime[]).map((v) => (
                <option key={v} value={v}>{PREP_TIME_LABELS[v]}</option>
              ))}
            </select>
          </label>
        </div>
        <button type="submit">Save recipe &amp; check pantry</button>
      </form>

      {error && <p className="error">{error}</p>}

      <div className="filter-bar">
        <div className="filter-group">
          <span className="filter-label">Meal:</span>
          <FilterChip active={mealFilter === 'all'} onClick={() => setMealFilter('all')}>All</FilterChip>
          {(Object.keys(MEAL_TYPE_LABELS) as MealType[]).map((v) => (
            <FilterChip key={v} active={mealFilter === v} onClick={() => setMealFilter(v)}>
              {MEAL_TYPE_LABELS[v]}
            </FilterChip>
          ))}
        </div>
        <div className="filter-group">
          <span className="filter-label">Prep:</span>
          <FilterChip active={prepFilter === 'all'} onClick={() => setPrepFilter('all')}>All</FilterChip>
          {(Object.keys(PREP_TIME_LABELS) as PrepTime[]).map((v) => (
            <FilterChip key={v} active={prepFilter === v} onClick={() => setPrepFilter(v)}>
              {PREP_TIME_LABELS[v]}
            </FilterChip>
          ))}
        </div>
        <span className="filter-count">
          {filteredRecipes.length} of {recipes.length}
        </span>
      </div>

      {filteredRecipes.length === 0 ? (
        <p className="muted">
          {recipes.length === 0
            ? 'No recipes yet. Add one above, or save some from Meal Ideas.'
            : 'No recipes match the current filters.'}
        </p>
      ) : (
        <ul className="recipe-list">
          {filteredRecipes.map((recipe) => {
            const diff = diffByRecipe[recipe.id]
            return (
              <li key={recipe.id} className="card">
                {recipe.imageUrl && <img src={recipe.imageUrl} alt={recipe.title} className="recipe-thumb" />}
                <h3>{recipe.title}</h3>
                <p className="recipe-tags">
                  <span className="tag">{MEAL_TYPE_LABELS[recipe.mealType]}</span>
                  <span className="tag">{PREP_TIME_LABELS[recipe.prepTime]}</span>
                  {recipe.sourceUrl && (
                    <a className="tag tag-link" href={recipe.sourceUrl} target="_blank" rel="noreferrer">source</a>
                  )}
                </p>
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
      )}
    </div>
  )
}

function FilterChip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button type="button" className={`filter-chip ${active ? 'active' : ''}`} onClick={onClick}>
      {children}
    </button>
  )
}
