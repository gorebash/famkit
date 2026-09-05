import type { MealSuggestion } from '../api/types'
import { IngredientList } from './IngredientList'

export function RecipeCard({ suggestion }: { suggestion: MealSuggestion }) {
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
    </div>
  )
}
