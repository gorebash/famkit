export interface PantryItem {
  partitionKey: string
  rowKey: string
  timestamp?: string
  name: string
  category: 'fridge' | 'pantry'
  quantity?: string | null
  unit?: string | null
  source: 'manual' | 'photo'
  addedDate: string
}

export interface RecipeIngredient {
  name: string
  quantity?: string | null
  unit?: string | null
}

export interface Recipe {
  id: string
  title: string
  ingredients: RecipeIngredient[]
  instructions?: string | null
  sourceUrl?: string | null
  createdDate: string
}

export interface IdentifiedIngredient {
  name: string
  estimatedQuantity?: string | null
  category?: string | null
}

export interface IdentifyResponse {
  ingredients: IdentifiedIngredient[]
}

export interface IngredientDiffResult {
  have: string[]
  missing: string[]
}

export interface MealSuggestion {
  spoonacularId: number
  title: string
  imageUrl?: string | null
  usedIngredients: string[]
  missedIngredients: string[]
}

export interface MealPlanResponse {
  suggestions: MealSuggestion[]
  groceryList: string[]
}
