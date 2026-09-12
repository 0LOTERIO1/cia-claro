import { useCallback, useRef, useState } from 'react'

export function useSpeechSynthesis() {
  const supported = typeof window !== 'undefined' && 'speechSynthesis' in window && 'SpeechSynthesisUtterance' in window
  const [speaking, setSpeaking] = useState(false)
  const currentRef = useRef<SpeechSynthesisUtterance | null>(null)

  const stop = useCallback(() => {
    if (!supported) return
    window.speechSynthesis.cancel()
    currentRef.current = null
    setSpeaking(false)
  }, [supported])

  const speak = useCallback(
    (text: string) => {
      if (!supported) return
      const value = text.trim()
      if (!value) return
      window.speechSynthesis.cancel()
      const utterance = new SpeechSynthesisUtterance(value)
      utterance.lang = 'pt-BR'
      utterance.rate = 1
      utterance.onend = () => {
        currentRef.current = null
        setSpeaking(false)
      }
      utterance.onerror = () => {
        currentRef.current = null
        setSpeaking(false)
      }
      currentRef.current = utterance
      setSpeaking(true)
      window.speechSynthesis.speak(utterance)
    },
    [supported],
  )

  return { supported, speaking, speak, stop }
}
