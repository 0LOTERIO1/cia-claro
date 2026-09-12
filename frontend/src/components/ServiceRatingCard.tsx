import { useState } from 'react'
import type { ServiceRatingDto } from '../types/api'
import { formatDateTime, formatStarScore } from '../services/labels'
import { StarRating } from './StarRating'

interface Props {
  canRate?: boolean
  rating?: ServiceRatingDto | null
  submitting?: boolean
  error?: string | null
  readOnly?: boolean
  waitingMessage?: string
  onSubmit?: (score: number, comment: string) => Promise<void> | void
}

export function ServiceRatingCard({
  canRate = false,
  rating,
  submitting = false,
  error,
  readOnly = false,
  waitingMessage = 'Este atendimento ainda não foi avaliado.',
  onSubmit,
}: Props) {
  const [score, setScore] = useState<number | null>(rating?.score ?? null)
  const [comment, setComment] = useState(rating?.comment ?? '')

  if (rating) {
    return (
      <section className="panel service-rating-card" aria-live="polite">
        <h2>Avaliação do cliente</h2>
        {!readOnly && <p className="ok">Obrigado pela sua avaliação.</p>}
        <p>
          <strong>{readOnly ? 'Nota' : 'Sua avaliação'}:</strong> {rating.score} / 5
        </p>
        <StarRating name="service-rating-readonly" value={rating.score} readOnly />
        <p className="hint">{formatStarScore(rating.score)}</p>
        {rating.comment ? (
          <div>
            <p>
              <strong>Comentário:</strong>
            </p>
            <p>{rating.comment}</p>
          </div>
        ) : (
          readOnly && <p className="hint">Sem comentário.</p>
        )}
        <p className="hint">{readOnly ? 'Data' : 'Enviada em'}: {formatDateTime(rating.createdAt)}</p>
      </section>
    )
  }

  if (readOnly || !canRate || !onSubmit) {
    return (
      <section className="panel service-rating-card">
        <h2>Avaliação do cliente</h2>
        <p className="hint">{waitingMessage}</p>
      </section>
    )
  }

  return (
    <section className="panel service-rating-card">
      <h2>Atendimento finalizado</h2>
      <form
        onSubmit={(event) => {
          event.preventDefault()
          if (!score || submitting) return
          void onSubmit(score, comment)
        }}
      >
        <fieldset>
          <legend>Como você avalia este atendimento?</legend>
          <StarRating name="service-rating-score" value={score} onChange={setScore} />
        </fieldset>
        <label htmlFor="service-rating-comment">Conte um pouco mais sobre sua experiência</label>
        <textarea
          id="service-rating-comment"
          maxLength={1000}
          rows={4}
          value={comment}
          onChange={(event) => setComment(event.target.value)}
          placeholder="Comentário opcional"
        />
        {error && (
          <p className="banner error" role="alert">
            {error}
          </p>
        )}
        <button type="submit" className="handoff-btn" disabled={!score || submitting}>
          {submitting ? 'Enviando...' : 'Enviar avaliação'}
        </button>
      </form>
    </section>
  )
}
