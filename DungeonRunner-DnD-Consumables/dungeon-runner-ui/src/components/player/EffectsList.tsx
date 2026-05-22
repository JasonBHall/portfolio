import { PlayerEffectDto } from '../../types'
import { Badge } from '../shared/Badge'

interface Props {
  effects: PlayerEffectDto[]
}

export function EffectsList({ effects }: Props) {
  if (effects.length === 0) {
    return (
      <p className="text-gray-500 text-sm text-center py-8">No active effects.</p>
    )
  }

  return (
    <div className="space-y-2">
      {effects.map((effect, i) => (
        <div key={i} className="bg-gray-800 rounded-lg p-3 border border-purple-900">
          <div className="flex items-center justify-between gap-2">
            <span className="text-purple-200 font-medium text-sm">{effect.name}</span>
            <Badge color="purple">{effect.remainingTurns} turn{effect.remainingTurns !== 1 ? 's' : ''}</Badge>
          </div>
          {effect.description && (
            <p className="text-gray-400 text-xs mt-1">{effect.description}</p>
          )}
        </div>
      ))}
    </div>
  )
}
