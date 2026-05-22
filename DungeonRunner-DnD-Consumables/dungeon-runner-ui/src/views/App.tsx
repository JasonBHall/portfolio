import { useState } from 'react'
import { useGameStore } from '../store/useGameStore'
import { TurnControls } from '../components/dm/TurnControls'
import { ActionLog } from '../components/dm/ActionLog'
import { PlayerCard } from '../components/dm/PlayerCard'
import { LootBoxCard } from '../components/dm/LootBoxCard'
import { PartyInventoryCard } from '../components/dm/PartyInventoryCard'
import { EncounterPanel } from '../components/dm/EncounterPanel'
import { CatalogManager } from '../components/dm/CatalogManager'
import { Button } from '../components/shared/Button'
import { timeModeLabel } from '../utils/item'

type Tab = 'party' | 'encounters' | 'catalog'

export default function DmView() {
  const {
    allCharacters, turnState, notifications, scenarios, dismissNotification,
    dmPartyShortRest, dmPartyLongRest, dmCreatePlayer,
  } = useGameStore()

  const [tab, setTab] = useState<Tab>('party')
  const [newUserId,    setNewUserId]    = useState('')
  const [newCharName,  setNewCharName]  = useState('')
  const [showCreatePlayer, setShowCreatePlayer] = useState(false)

  const handleCreatePlayer = async () => {
    const uid = newUserId.trim().toLowerCase()
    if (!uid) return
    await dmCreatePlayer(uid, newCharName.trim())
    setNewUserId(''); setNewCharName(''); setShowCreatePlayer(false)
  }

  return (
    <div className="min-h-screen max-w-3xl mx-auto px-4 py-4 flex flex-col gap-4">

      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-gray-100 font-semibold text-lg">DM Console</h1>
          {turnState && (
            <p className="text-gray-500 text-xs">
              Turn {turnState.currentTurn} · {timeModeLabel(turnState.timeMode)}
              {turnState.activeScenario && (() => {
                const active = scenarios.find(s => s.id === turnState.activeScenario)
                const name = active?.name ?? turnState.activeScenario
                return (
                  <>
                    {' · '}
                    <span className="text-indigo-400">⚡ {name}</span>
                    {turnState.activeScenarioTheme && (
                      <span className="text-gray-600"> ({turnState.activeScenarioTheme} theme)</span>
                    )}
                  </>
                )
              })()}
            </p>
          )}
        </div>
        <span className="text-xs text-gray-600">
          {allCharacters.length} player{allCharacters.length !== 1 ? 's' : ''}
        </span>
      </header>

      {notifications.length > 0 && (
        <div className="space-y-1">
          {notifications.map((n, i) => (
            <div key={i} className="bg-indigo-900/40 border border-indigo-700/40 rounded-lg
                                    px-4 py-2 flex items-center justify-between">
              <span className="text-indigo-200 text-sm">{n}</span>
              <button onClick={() => dismissNotification(i)}
                className="text-indigo-400 hover:text-indigo-200 ml-4 text-lg leading-none">×</button>
            </div>
          ))}
        </div>
      )}

      <TurnControls />

      <nav className="flex border-b border-gray-800">
        {(['party', 'encounters', 'catalog'] as Tab[]).map(t => (
          <button key={t} onClick={() => setTab(t)}
            className={`px-4 py-2 text-sm font-medium capitalize transition-colors border-b-2 -mb-px ${
              tab === t
                ? 'text-indigo-400 border-indigo-500'
                : 'text-gray-500 border-transparent hover:text-gray-300'
            }`}>
            {t}
          </button>
        ))}
      </nav>

      <div className="flex-1 space-y-3">
        {tab === 'party' && (
          <>
            <div className="bg-gray-900 border border-gray-800 rounded-xl p-4 space-y-3">
              <div className="flex items-center justify-between">
                <p className="text-gray-400 text-xs uppercase tracking-wide">Whole Party</p>
                <Button size="sm" variant="ghost" onClick={() => setShowCreatePlayer(v => !v)}>
                  {showCreatePlayer ? 'Cancel' : '+ Add Player'}
                </Button>
              </div>

              <div className="flex gap-2">
                <Button variant="secondary" className="flex-1"
                  onClick={() => { if (confirm('Short rest all players?')) dmPartyShortRest() }}>
                  Party Short Rest
                </Button>
                <Button variant="secondary" className="flex-1"
                  onClick={() => { if (confirm('Long rest all players?')) dmPartyLongRest() }}>
                  Party Long Rest
                </Button>
              </div>

              {showCreatePlayer && (
                <div className="flex gap-2 items-end pt-1 border-t border-gray-800">
                  <div className="flex-1">
                    <p className="text-gray-500 text-xs mb-1">User ID</p>
                    <input type="text" placeholder="dave" value={newUserId}
                      onChange={e => setNewUserId(e.target.value)}
                      onKeyDown={e => e.key === 'Enter' && handleCreatePlayer()}
                      className="w-full bg-gray-800 border border-gray-700 rounded px-2 py-1.5
                                 text-gray-100 text-sm placeholder-gray-600
                                 focus:outline-none focus:border-indigo-500" />
                  </div>
                  <div className="flex-1">
                    <p className="text-gray-500 text-xs mb-1">Character Name</p>
                    <input type="text" placeholder="Aldric Stonefist" value={newCharName}
                      onChange={e => setNewCharName(e.target.value)}
                      onKeyDown={e => e.key === 'Enter' && handleCreatePlayer()}
                      className="w-full bg-gray-800 border border-gray-700 rounded px-2 py-1.5
                                 text-gray-100 text-sm placeholder-gray-600
                                 focus:outline-none focus:border-indigo-500" />
                  </div>
                  <Button onClick={handleCreatePlayer} disabled={!newUserId.trim()}>Create</Button>
                </div>
              )}

              {showCreatePlayer && newUserId.trim() && (
                <p className="text-gray-600 text-xs">
                  Player URL:{' '}
                  <span className="text-gray-500 font-mono">
                    {window.location.origin}/{newUserId.trim().toLowerCase()}
                  </span>
                </p>
              )}
            </div>

            <LootBoxCard />
            <PartyInventoryCard />

            {allCharacters.length === 0 ? (
              <p className="text-gray-600 text-sm text-center py-8">
                No players yet. Create one above or have them join via their URL.
              </p>
            ) : (
              allCharacters.map(c => <PlayerCard key={c.userId} character={c} />)
            )}
          </>
        )}

        {tab === 'encounters' && <EncounterPanel />}
        {tab === 'catalog'    && <CatalogManager />}
      </div>

      <ActionLog />
    </div>
  )
}
