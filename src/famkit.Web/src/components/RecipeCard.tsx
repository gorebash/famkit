import type { MealSuggestion } from '../api/types'
import { IngredientList } from './IngredientList'

interface RecipeCardProps {
  suggestion: MealSuggestion
  onSave?: (suggestion: MealSuggestion) => void
  saveState?: 'idle' | 'saving' | 'saved' | 'error'
  saveError?: string | null
}

export function RecipeCard({ suggestion, onSave, saveState = 'idle', saveError }: RecipeCardProps) {
  const buttonLabel =
    saveState === 'saving' ? 'Saving...' :
    saveState === 'saved' ? 'Saved ✓' :
    saveState === 'error' ? 'Retry save' :
    'Save to recipes'

  return (
    <div className="card recipe-card">
      {suggestion.imageUrl && <img src={suggestion.imageUrl} alt={suggestion.title} />}
      <h3>{suggestion.title}</h3>
      <div className="recipe-card-columns">
        <div>
          <h4>You have</h4>
          <IngredientList items={suggestion.usedIngredients} emptyLabel="Nothing matched" />
        </div>
        <div>
          <h4>Missing</h4>
          <IngredientList items={suggestion.missedIngredients} emptyLabel="Nothing missing!" />
        </div>
      </div>
      {onSave && (
        <div className="recipe-card-actions">
          <button
            onClick={() => onSave(suggestion)}
            disabled={saveState === 'saving' || saveState === 'saved'}
          >
            {buttonLabel}
          </button>
          {saveState === 'error' && saveError && <p className="error">{saveError}</p>}
        </div>
      )}
    </div>
  )
}
