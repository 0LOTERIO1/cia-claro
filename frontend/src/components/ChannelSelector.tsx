import type { ChannelType } from '../types/api'
import { formatChannel } from '../services/labels'

interface Props {
  channel: ChannelType
  disabled?: boolean
  onChange: (channel: ChannelType) => void
}

export function ChannelSelector({ channel, disabled, onChange }: Props) {
  return (
    <div className="channel-selector" role="group" aria-label="Canal de atendimento">
      <button
        type="button"
        className={channel === 'AppClaro' ? 'is-active app' : 'app'}
        disabled={disabled}
        onClick={() => onChange('AppClaro')}
      >
        {formatChannel('AppClaro')}
      </button>
      <button
        type="button"
        className={channel === 'WhatsApp' ? 'is-active wa' : 'wa'}
        disabled={disabled}
        onClick={() => onChange('WhatsApp')}
      >
        {formatChannel('WhatsApp')}
      </button>
    </div>
  )
}
