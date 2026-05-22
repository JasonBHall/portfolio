import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'

export function RestControls() {
  const { shortRest, longRest } = useGameStore()

  return (
    <div className="bg-gray-800 rounded-lg p-4 border border-gray-700">
      <p className="text-gray-400 text-xs uppercase tracking-wide mb-3">Rest</p>
      <div className="flex gap-3">
        <Button variant="secondary" onClick={shortRest} className="flex-1">
          Short Rest
        </Button>
        <Button variant="secondary" onClick={longRest} className="flex-1">
          Long Rest
        </Button>
      </div>
      <p className="text-gray-500 text-xs mt-2">
        Short rest recovers short-rest abilities. Long rest recovers everything.
      </p>
    </div>
  )
}
