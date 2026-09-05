import { NavLink, Route, Routes } from 'react-router-dom'
import { PantryPage } from './pages/PantryPage'
import { MealIdeasPage } from './pages/MealIdeasPage'
import { AddRecipePage } from './pages/AddRecipePage'
import { GroceryListPage } from './pages/GroceryListPage'

export default function App() {
  return (
    <div className="app">
      <nav className="nav">
        <span className="brand">FamKit</span>
        <NavLink to="/" end>
          Pantry
        </NavLink>
        <NavLink to="/meals">Meal Ideas</NavLink>
        <NavLink to="/recipes">Recipes</NavLink>
        <NavLink to="/grocery-list">Grocery List</NavLink>
      </nav>
      <main className="content">
        <Routes>
          <Route path="/" element={<PantryPage />} />
          <Route path="/meals" element={<MealIdeasPage />} />
          <Route path="/recipes" element={<AddRecipePage />} />
          <Route path="/grocery-list" element={<GroceryListPage />} />
        </Routes>
      </main>
    </div>
  )
}
