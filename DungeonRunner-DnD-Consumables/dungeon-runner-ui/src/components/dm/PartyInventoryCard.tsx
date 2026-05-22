import { useState } from 'react'
import type { ItemDto } from '../../types'
import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'
import { Badge } from '../shared/Badge'
import { ItemIcon } from '../shared/ItemIcon'
import { CatalogPickerModal } from '../shared/CatalogPickerModal'
import { itemLabel } from '../../utils/item'

export function PartyInventoryCard() {
  const {
    party, catalog, scenarios,
    dmAddPartyItem, dmAddPartyItemFromCatalog,
    dmEditPartyItem, dmDeletePartyItem,
  } = useGameStore()

  const [customName, setCustomName] = useState('')
  const [customQty,  setCustomQty]  = useState(1)
  const [expanded,   setExpanded]   = useState(true)
  const [showGiveModal, setShowGiveModal] = useState(false)

  if (!party) return null

  return (
    <>
      <div className="bg-gray-900 border border-teal-900/40 rounded-xl overflow-hidden">
        <div className="flex items-center justify-between px-4 py-3 border-b border-gray-800">
          <button onClick={() => setExpanded(e => !e)}
            className="flex items-center gap-3 flex-1 text-left">
            <span className="text-teal-400 text-xs uppercase tracking-wide font-medium">⚔ Party Inventory</span>
            <span className="text-gray-600 text-xs">{party.inventory.length} item{party.inventory.length !== 1 ? 's' : ''}</span>
            <span className={`text-gray-500 text-xs ml-auto transition-transform ${expanded ? 'rotate-180' : ''}`}>▲</span>
          </button>
          <div className="flex gap-2 ml-3">
            <Button size="sm" variant="secondary" onClick={() => setShowGiveModal(true)}
              disabled={catalog.length === 0}>
              Give Item
            </Button>
          </div>
        </div>

        {expanded && (
          <div className="px-4 py-3 space-y-3">
            <div className="flex gap-2">
              <input type="text" placeholder="Add item to party inventory..." value={customName}
                onChange={e => setCustomName(e.target.value)}
                onKeyDown={e => {
                  if (e.key === 'Enter' && customName.trim()) {
                    dmAddPartyItem(customName.trim(), customQty); setCustomName(''); setCustomQty(1)
                  }
                }}
                className="flex-1 bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                           text-gray-100 text-sm placeholder-gray-600
                           focus:outline-none focus:border-teal-500" />
              <input type="number" min={1} value={customQty}
                onChange={e => setCustomQty(Math.max(1, Number(e.target.value)))}
                className="w-14 bg-gray-800 border border-gray-700 rounded px-2 py-1.5
                           text-gray-100 text-sm text-center focus:outline-none focus:border-teal-500" />
              <Button disabled={!customName.trim()}
                onClick={() => { dmAddPartyItem(customName.trim(), customQty); setCustomName(''); setCustomQty(1) }}>
                Add
              </Button>
            </div>

            {party.inventory.length === 0 ? (
              <p className="text-gray-600 text-sm text-center py-3">Party inventory is empty.</p>
            ) : (
              <div className="space-y-1.5">
                {party.inventory.map(item => (
                  <PartyItemRow key={item.id} item={item} scenarios={scenarios}
                    onEdit={dmEditPartyItem}
                    onDelete={dmDeletePartyItem} />
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {showGiveModal && (
        <CatalogPickerModal catalog={catalog}
          title="Add to Party Inventory"
          actionLabel="Add to Party"
          accentSelectedClass="bg-teal-900/40 border-teal-600"
          onPick={(templateId, qty) => {
            dmAddPartyItemFromCatalog(templateId, qty)
            setShowGiveModal(false)
          }}
          onClose={() => setShowGiveModal(false)} />
      )}
    </>
  )
}

function PartyItemRow({
  item, scenarios, onEdit, onDelete,
}: {
  item: ItemDto
  scenarios: import('../../types').ScenarioDto[]
  onEdit: (id: string, qty: number | undefined, playerDesc: string, dmDesc: string) => Promise<void>
  onDelete: (id: string) => Promise<void>
}) {
  const [editing,    setEditing]    = useState(false)
  const [editQty,    setEditQty]    = useState(item.quantity)
  const [playerDesc, setPlayerDesc] = useState(item.playerDescription ?? '')
  const [dmDesc,     setDmDesc]     = useState(item.dmDescription ?? '')

  const label = itemLabel(item, item.quantity)
  const scenarioLabel = item.scenario
    ? (scenarios.find(s => s.id === item.scenario)?.name ?? item.scenario)
    : null

  return (
    <div className="bg-gray-800/60 rounded-lg px-3 py-2 border border-teal-900/30">
      <div className="flex items-center gap-2">
        <ItemIcon iconUrl={item.iconUrl} alt={item.name} />
        <span className="flex-1 text-teal-100 text-sm truncate">{label}</span>
        <Badge color="gray">{item.category}</Badge>
        {scenarioLabel && <Badge color="purple">⚡ {scenarioLabel}</Badge>}
        {item.claimable && <Badge color="green">claimable</Badge>}
        <span className="text-teal-200 font-mono text-xs">×{item.quantity}</span>
        <Button size="sm" variant="ghost" onClick={() => setEditing(e => !e)}>{editing ? 'done' : 'edit'}</Button>
        <button
          onClick={() => { if (confirm(`Remove ${label} from party inventory?`)) onDelete(item.id) }}
          className="w-6 h-6 flex items-center justify-center rounded bg-red-900 hover:bg-red-800
                     text-red-100 text-xs font-bold transition-colors shrink-0">
          ×
        </button>
      </div>

      {item.playerDescription && !editing && (
        <p className="text-xs text-gray-400 italic mt-1">{item.playerDescription}</p>
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
          <Button size="sm" onClick={() => { onEdit(item.id, editQty, playerDesc, dmDesc); setEditing(false) }}>
            Save
          </Button>
        </div>
      )}
    </div>
  )
}
