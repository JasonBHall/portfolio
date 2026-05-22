import { useState } from 'react'
import type { LootItemDto } from '../../types'
import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'
import { Badge } from '../shared/Badge'
import { ItemIcon } from '../shared/ItemIcon'
import { CatalogPickerModal } from '../shared/CatalogPickerModal'

export function LootBoxCard() {
  const {
    party, catalog, scenarios,
    dmAddLoot, dmAddLootFromCatalog, dmClearLootBox,
    dmEditLootItem, dmDeleteLootItem, claimLootToParty,
  } = useGameStore()

  const [customName, setCustomName] = useState('')
  const [customQty,  setCustomQty]  = useState(1)
  const [expanded,   setExpanded]   = useState(true)
  const [showGiveModal, setShowGiveModal] = useState(false)

  if (!party) return null
  const lootBox = party.lootBox

  return (
    <>
      <div className="bg-gray-900 border border-amber-900/40 rounded-xl overflow-hidden">
        <div className="flex items-center justify-between px-4 py-3 border-b border-gray-800">
          <button onClick={() => setExpanded(e => !e)}
            className="flex items-center gap-3 flex-1 text-left">
            <span className="text-amber-400 text-xs uppercase tracking-wide font-medium">⚔ Loot Box</span>
            <span className="text-gray-600 text-xs">{lootBox.length} item{lootBox.length !== 1 ? 's' : ''}</span>
            <span className={`text-gray-500 text-xs ml-auto transition-transform ${expanded ? 'rotate-180' : ''}`}>▲</span>
          </button>
          <div className="flex gap-2 ml-3">
            <Button size="sm" variant="secondary" onClick={() => setShowGiveModal(true)}
              disabled={catalog.length === 0}>
              Give Item
            </Button>
            {lootBox.length > 0 && (
              <Button size="sm" variant="danger"
                onClick={() => { if (confirm('Clear entire loot box?')) dmClearLootBox() }}>
                Clear
              </Button>
            )}
          </div>
        </div>

        {expanded && (
          <div className="px-4 py-3 space-y-3">
            <div className="flex gap-2">
              <input type="text" placeholder="Drop custom loot..." value={customName}
                onChange={e => setCustomName(e.target.value)}
                onKeyDown={e => {
                  if (e.key === 'Enter' && customName.trim()) {
                    dmAddLoot(customName.trim(), customQty); setCustomName(''); setCustomQty(1)
                  }
                }}
                className="flex-1 bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                           text-gray-100 text-sm placeholder-gray-600
                           focus:outline-none focus:border-indigo-500" />
              <input type="number" min={1} value={customQty}
                onChange={e => setCustomQty(Math.max(1, Number(e.target.value)))}
                className="w-14 bg-gray-800 border border-gray-700 rounded px-2 py-1.5
                           text-gray-100 text-sm text-center focus:outline-none focus:border-indigo-500" />
              <Button disabled={!customName.trim()}
                onClick={() => { dmAddLoot(customName.trim(), customQty); setCustomName(''); setCustomQty(1) }}>
                Drop
              </Button>
            </div>

            {lootBox.length === 0 ? (
              <p className="text-gray-600 text-sm text-center py-3">Loot box is empty.</p>
            ) : (
              <div className="space-y-1.5">
                {lootBox.map(loot => (
                  <LootBoxRow key={loot.id} loot={loot} scenarios={scenarios}
                    onMoveToParty={claimLootToParty}
                    onEdit={dmEditLootItem}
                    onDelete={dmDeleteLootItem} />
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {showGiveModal && (
        <CatalogPickerModal catalog={catalog}
          title="Drop Item into Loot Box"
          actionLabel="Drop into Loot Box"
          accentSelectedClass="bg-amber-900/30 border-amber-600"
          onPick={(templateId, qty) => {
            dmAddLootFromCatalog(templateId, qty)
            setShowGiveModal(false)
          }}
          onClose={() => setShowGiveModal(false)} />
      )}
    </>
  )
}

function LootBoxRow({
  loot, scenarios, onMoveToParty, onEdit, onDelete,
}: {
  loot: LootItemDto
  scenarios: import('../../types').ScenarioDto[]
  onMoveToParty: (id: string, qty: number) => Promise<void>
  onEdit: (id: string, qty: number | undefined, playerDesc: string, dmDesc: string) => Promise<void>
  onDelete: (id: string) => Promise<void>
}) {
  const [qty,        setQty]        = useState(1)
  const [editing,    setEditing]    = useState(false)
  const [editQty,    setEditQty]    = useState(loot.quantity)
  const [playerDesc, setPlayerDesc] = useState(loot.playerDescription ?? '')
  const [dmDesc,     setDmDesc]     = useState(loot.dmDescription ?? '')
  const scenarioLabel = loot.scenario
    ? (scenarios.find(s => s.id === loot.scenario)?.name ?? loot.scenario)
    : null

  return (
    <div className="bg-gray-800 rounded-lg px-3 py-2 border border-gray-700">
      <div className="flex items-center gap-2">
        <ItemIcon iconUrl={loot.iconUrl} alt={loot.name} />
        <span className="flex-1 text-amber-200 text-sm truncate">{loot.name}</span>
        {scenarioLabel && <Badge color="purple">⚡ {scenarioLabel}</Badge>}
        {!loot.claimable && (
          <span className="text-gray-600 text-xs italic shrink-0">party only</span>
        )}
        <span className="text-gray-500 font-mono text-xs">×{loot.quantity}</span>
        <input type="number" min={1} max={loot.quantity} value={qty}
          onChange={e => setQty(Math.min(loot.quantity, Math.max(1, Number(e.target.value))))}
          className="w-12 bg-gray-700 border border-gray-600 rounded px-1.5 py-1
                     text-gray-100 text-xs text-center focus:outline-none focus:border-indigo-500" />
        <Button size="sm" variant="secondary" onClick={() => onMoveToParty(loot.id, qty)}>→ Party</Button>
        <Button size="sm" variant="ghost" onClick={() => setEditing(e => !e)}>{editing ? 'done' : 'edit'}</Button>
        <button
          onClick={() => { if (confirm(`Remove ${loot.name}?`)) onDelete(loot.id) }}
          className="w-6 h-6 flex items-center justify-center rounded bg-red-900 hover:bg-red-800
                     text-red-100 text-xs font-bold transition-colors shrink-0">
          ×
        </button>
      </div>

      {loot.playerDescription && !editing && (
        <p className="text-xs text-gray-500 italic mt-1">{loot.playerDescription}</p>
      )}

      {editing && (
        <div className="mt-2 space-y-2 border-t border-gray-700 pt-2">
          <div className="flex gap-2 items-center">
            <label className="text-gray-500 text-xs w-10 shrink-0">Qty</label>
            <input type="number" min={0} value={editQty}
              onChange={e => setEditQty(Number(e.target.value))}
              className="w-16 bg-gray-700 border border-gray-600 rounded px-2 py-1
                         text-gray-100 text-xs text-center focus:outline-none focus:border-indigo-500" />
          </div>
          <div>
            <label className="text-gray-500 text-xs block mb-1">Player description</label>
            <textarea value={playerDesc} onChange={e => setPlayerDesc(e.target.value)} rows={2}
              className="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1
                         text-gray-100 text-xs resize-none focus:outline-none focus:border-indigo-500" />
          </div>
          <div>
            <label className="text-purple-400 text-xs block mb-1">DM description</label>
            <textarea value={dmDesc} onChange={e => setDmDesc(e.target.value)} rows={2}
              className="w-full bg-gray-700 border border-purple-900 rounded px-2 py-1
                         text-gray-100 text-xs resize-none focus:outline-none focus:border-purple-700" />
          </div>
          <Button size="sm" onClick={() => { onEdit(loot.id, editQty, playerDesc, dmDesc); setEditing(false) }}>
            Save
          </Button>
        </div>
      )}
    </div>
  )
}
