import { useState } from 'react'
import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'
import { ScenarioManager } from './ScenarioManager'

export function TurnControls() {
  const {
    turnState, scenarios,
    dmAdvanceTurn, dmResetTurn, dmChangeTimeMode,
    dmStartScenario, dmEndScenario,
  } = useGameStore()

  const [showManager, setShowManager] = useState(false)

  if (!turnState) return null

  const modes = [
    { value: 'Dungeon',        label: 'Dungeon',   sub: '10 min/turn' },
    { value: 'UnknownOpenAir', label: 'Open Air?', sub: '1 hr/turn'   },
    { value: 'OpenAir',        label: 'Open Air',  sub: '1 day/turn'  },
  ]

  const currentPascal = turnState.timeMode.charAt(0).toUpperCase() + turnState.timeMode.slice(1)
  const activeId = turnState.activeScenario
  const activeScenario = scenarios.find(s => s.id === activeId)

  return (
    <>
      <div className="bg-gray-900 border border-gray-800 rounded-xl p-4 space-y-4">
        {/* Turn info + controls */}
        <div className="flex items-center justify-between">
          <div>
            <p className="text-gray-400 text-xs uppercase tracking-wide">Current Turn</p>
            <p className="text-gray-100 text-2xl font-mono font-bold">{turnState.currentTurn}</p>
          </div>
          <div className="flex gap-2">
            <Button variant="ghost"
              onClick={() => { if (confirm('Reset turn counter to 1?')) dmResetTurn() }}>
              Reset
            </Button>
            <Button onClick={dmAdvanceTurn} className="px-5 py-2">Advance Turn</Button>
          </div>
        </div>

        {/* Time mode selector */}
        <div>
          <p className="text-gray-400 text-xs uppercase tracking-wide mb-2">Time Mode</p>
          <div className="grid grid-cols-3 gap-2">
            {modes.map(mode => (
              <button key={mode.value} onClick={() => dmChangeTimeMode(mode.value)}
                className={`rounded-lg p-2 text-center border transition-colors ${
                  currentPascal === mode.value
                    ? 'bg-indigo-900/50 border-indigo-600 text-indigo-300'
                    : 'bg-gray-800 border-gray-700 text-gray-400 hover:border-gray-600'
                }`}>
                <div className="text-sm font-medium">{mode.label}</div>
                <div className="text-xs opacity-60">{mode.sub}</div>
              </button>
            ))}
          </div>
        </div>

        {/* Scenario — dropdown + Manage link, or active strip */}
        <div className="border-t border-gray-800 pt-4">
          <div className="flex items-center justify-between mb-2">
            <p className="text-gray-400 text-xs uppercase tracking-wide">Scenario</p>
            <button onClick={() => setShowManager(true)}
              className="text-xs text-indigo-400 hover:text-indigo-300">
              Manage scenarios…
            </button>
          </div>

          {activeId ? (
            <div className="flex items-center gap-3 bg-indigo-900/30 border border-indigo-700/40
                            rounded-lg px-3 py-2">
              <span className="text-indigo-300 text-sm font-medium flex-1">
                ⚡ {activeScenario?.name ?? activeId}
                {activeScenario?.theme && (
                  <span className="text-gray-500 ml-2 text-xs">({activeScenario.theme} theme)</span>
                )}
              </span>
              <Button size="sm" variant="danger"
                onClick={() => {
                  const label = activeScenario?.name ?? activeId
                  if (confirm(`End scenario "${label}"?\n\nAll items tagged with this scenario will be deleted from every inventory. Catalog templates and the scenario record stay intact.`))
                    dmEndScenario()
                }}>
                End Scenario
              </Button>
            </div>
          ) : (
            <StartScenarioPicker scenarios={scenarios} onStart={dmStartScenario}
              onOpenManager={() => setShowManager(true)} />
          )}
        </div>
      </div>

      {showManager && <ScenarioManager onClose={() => setShowManager(false)} />}
    </>
  )
}

function StartScenarioPicker({
  scenarios, onStart, onOpenManager,
}: {
  scenarios: import('../../types').ScenarioDto[]
  onStart: (id: string) => Promise<void>
  onOpenManager: () => void
}) {
  const [selected, setSelected] = useState('')

  if (scenarios.length === 0) {
    return (
      <div className="text-sm text-gray-500 flex items-center gap-2">
        <span>No scenarios defined.</span>
        <button onClick={onOpenManager} className="text-indigo-400 hover:text-indigo-300 underline">
          Create one
        </button>
      </div>
    )
  }

  return (
    <div className="flex gap-2">
      <select value={selected} onChange={e => setSelected(e.target.value)}
        className="flex-1 bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                   text-gray-100 text-sm focus:outline-none focus:border-indigo-500">
        <option value="">Select scenario…</option>
        {scenarios.map(s => (
          <option key={s.id} value={s.id}>
            {s.name}{s.theme ? ` (${s.theme} theme)` : ''}
          </option>
        ))}
      </select>
      <Button onClick={() => { if (selected) { onStart(selected); setSelected('') } }}
        disabled={!selected}>
        Start
      </Button>
    </div>
  )
}
