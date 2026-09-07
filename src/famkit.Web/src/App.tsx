import { NavLink, Route, Routes } from 'react-router-dom'
import { HomePage } from './pages/HomePage'
import { RecipesPage } from './pages/RecipesPage'

export default function App() {
  return (
    <div className="app">
      <nav className="nav">
        <span className="brand">FamKit</span>
        <NavLink to="/" end>
          Home
        </NavLink>
        <NavLink to="/recipes">Recipes</NavLink>
      </nav>
      <main className="content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/recipes" element={<RecipesPage />} />
        </Routes>
      </main>
    </div>
  )
}
