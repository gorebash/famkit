interface IngredientListProps {
  items: string[]
  emptyLabel?: string
}

export function IngredientList({ items, emptyLabel = 'None' }: IngredientListProps) {
  if (items.length === 0) {
    return <p className="muted">{emptyLabel}</p>
  }

  return (
    <ul className="ingredient-list">
      {items.map((item) => (
        <li key={item}>{item}</li>
      ))}
    </ul>
  )
}
