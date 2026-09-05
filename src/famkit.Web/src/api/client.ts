import type {
  PantryItem,
  Recipe,
  RecipeIngredient,
  IdentifyResponse,
  IngredientDiffResult,
  MealPlanResponse,
} from './types'

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7071/api'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, init)
  if (!res.ok) {
    const message = await res.text().catch(() => res.statusText)
    throw new Error(message || `Request to ${path} failed with ${res.status}`)
  }
  if (res.status === 204) {
    return undefined as T
  }
  return (await res.json()) as T
}

export const api = {
  getPantry: () => request<PantryItem[]>('/pantry'),

  addPantryItem: (item: { name: string; category: 'fridge' | 'pantry'; quantity?: string; unit?: string; source?: 'manual' | 'photo' }) =>
    request<PantryItem>('/pantry', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(item),
    }),

  deletePantryItem: (id: string) =>
    request<void>(`/pantry/${encodeURIComponent(id)}`, { method: 'DELETE' }),

  identifyIngredients: (file: File) =>
    request<IdentifyResponse>('/vision/identify', {
      method: 'POST',
      headers: { 'Content-Type': file.type || 'image/jpeg' },
      body: file,
    }),

  getRecipes: () => request<Recipe[]>('/recipes'),

  addRecipe: (recipe: { title: string; ingredients: RecipeIngredient[]; instructions?: string; sourceUrl?: string }) =>
    request<Recipe>('/recipes', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(recipe),
    }),

  deleteRecipe: (id: string) => request<void>(`/recipes/${encodeURIComponent(id)}`, { method: 'DELETE' }),

  evaluateRecipe: (body: { recipeId?: string; ingredients?: RecipeIngredient[] }) =>
    request<IngredientDiffResult>('/recipes/evaluate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }),

  suggestMealPlan: () => request<MealPlanResponse>('/mealplan/suggest', { method: 'POST' }),
}
