import { useState } from 'react'
import { STAR_SCORE_LABELS, formatStarScore } from '../services/labels'

const SCORES = [1, 2, 3, 4, 5] as const

interface Props {
  name: string
  value: number | null
  onChange?: (score: number) => void
  readOnly?: boolean
}

export function StarRating({ name, value, onChange, readOnly = false }: Props) {
  const [hover, setHover] = useState<number | null>(null)
  const shown = hover ?? value ?? 0

  if (readOnly) {
    const score = value ?? 0
    return (
      <p className="star-rating-readonly" aria-label={score ? formatStarScore(score) : 'Sem avaliação'}>
        {SCORES.map((item) => (
          <span key={item} className={item <= score ? 'star is-filled' : 'star'} aria-hidden="true">
            {item <= score ? '★' : '☆'}
          </span>
        ))}
        {score > 0 && <span className="sr-only">{formatStarScore(score)}</span>}
      </p>
    )
  }

  return (
    <div
      className="star-rating"
      role="radiogroup"
      aria-label="Como você avalia este atendimento?"
      onMouseLeave={() => setHover(null)}
    >
      {SCORES.map((score) => {
        const selected = value === score
        const filled = shown >= score
        return (
          <label
            key={score}
            className={filled ? 'star-option is-filled' : 'star-option'}
            onMouseEnter={() => setHover(score)}
          >
            <input
              type="radio"
              name={name}
              value={score}
              checked={selected}
              onChange={() => onChange?.(score)}
              aria-label={formatStarScore(score)}
            />
            <span className="star" aria-hidden="true">
              {filled ? '★' : '☆'}
            </span>
            <span className="star-caption">
              {score} — {STAR_SCORE_LABELS[score]}
            </span>
          </label>
        )
      })}
    </div>
  )
}
