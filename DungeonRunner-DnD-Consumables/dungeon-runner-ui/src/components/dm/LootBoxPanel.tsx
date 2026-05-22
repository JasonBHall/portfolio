import { useState } from 'react'
import { LootItemDto, ItemDto } from '../../types'
import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'

export function LootBoxPanel() {
  const {
    party, catalog,
    dmAddLoot, dmAddLootFromCatalog, dmClearLootBox,
    dmEditLootItem, dmEditPartyItem,
    claimLootToParty,
  } = useGameStore()

  const [customName, setCustomName] = useState('')
  const [customQty, setCustomQty] = useState(1)
  const [catalogId, setCatalogId] = useState('')
  const [catalogQty, setCatalogQty] = useState(1)

  if (!party) return null

  const handleAddCustom = () => {
    if (!customName.trim()) return
    dmAddLoot(customName.trim(), customQty)
    setCustomName('')
    setCustomQty(1)
  }

  const handleAddFromCatalog = () => {
    if (!catalogId) return
    dmAddLootFromCatalog(catalogId, catalogQty)
    setCatalogId('')
    setCatalogQty(1)
  }

  return (
    <div className="space-y-4">

      {/* Add custom loot */}
      <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
        <p className="text-gray-400 text-xs uppercase tracking-wide mb-3">Add Custom Loot</p>
        <div className="flex gap-2">
          <input type="text" placeholder="Item name" value={customName}
            onChange={e => setCustomName(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleAddCustom()}
            className="flex-1 bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                       text-gray-100 text-sm placeholder-gray-600
                       focus:outline-none focus:border-indigo-500" />
          <input type="number" min={1} value={customQty}
            onChange={e => setCustomQty(Math.max(1, Number(e.target.value)))}
            className="w-16 bg-gray-800 border border-gray-700 rounded px-2 py-1.5
                       text-gray-100 text-sm text-center focus:outline-none focus:border-indigo-500" />
          <Button onClick={handleAddCustom} disabled={!customName.trim()}>Drop</Button>
        </div>
      </div>

      {/* Add from catalog */}
      {catalog.length > 0 && (
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
          <p className="text-gray-400 text-xs uppercase tracking-wide mb-3">Add from Catalog</p>
          <div className="flex gap-2">
            <select value={catalogId} onChange={e => setCatalogId(e.target.value)}
              className="flex-1 bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                         text-gray-300 text-sm focus:outline-none focus:border-indigo-500">
              <option value="">Select item...</option>
              {catalog.map(t => (
                <option key={t.id} value={t.id}>{t.displayName ?? t.name}</option>
              ))}
            </select>
            <input type="number" min={1} value={catalogQty}
              onChange={e => setCatalogQty(Math.max(1, Number(e.target.value)))}
              className="w-16 bg-gray-800 border border-gray-700 rounded px-2 py-1.5
                         text-gray-100 text-sm text-center focus:outline-none focus:border-indigo-500" />
            <Button onClick={handleAddFromCatalog} disabled={!catalogId}>Drop</Button>
          </div>
        </div>
      )}

      {/* Loot Box */}
      <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div className="px-4 py-2 border-b border-gray-800 flex items-center justify-between">
          <p className="text-gray-400 text-xs uppercase tracking-wide">Loot Box</p>
          {party.lootBox.length > 0 && (
            <Button size="sm" variant="danger"
              onClick={() => { if (confirm('Clear entire loot box?')) dmClearLootBox() }}>
              Clear All
            </Button>
          )}
        </div>

        {party.lootBox.length === 0 ? (
          <p className="text-gray-600 text-sm text-center py-6">Empty.</p>
        ) : (
          <div className="divide-y divide-gray-800">
            {party.lootBox.map(loot => (
              <LootBoxRow key={loot.id} loot={loot} onMoveToParty={claimLootToParty}
                onEdit={dmEditLootItem} />
            ))}
          </div>
        )}
      </div>

      {/* Party Inventory */}
      {party.inventory.length > 0 && (
        <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
          <div className="px-4 py-2 border-b border-gray-800">
            <p className="text-gray-400 text-xs uppercase tracking-wide">Party Inventory</p>
          </div>
          <div className="divide-y divide-gray-800">
            {party.inventory.map(item => (
              <PartyItemRow key={item.id} item={item} onEdit={dmEditPartyItem} />
            ))}
          </div>
        </div>
      )}
    </div>
  )
}

// ---------------------------------------------------------
// Loot Box Row with DM edit
// ---------------------------------------------------------
function LootBoxRow({
  loot, onMoveToParty, onEdit,
}: {
  loot: LootItemDto
  onMoveToParty: (id: string, qty: number) => Promise<void>
  onEdit: (id: string, qty: number | undefined, playerDesc: string, dmDesc: string) => Promise<void>
}) {
  const [qty, setQty] = useState(1)
  const [editing, setEditing] = useState(false)
  const [editQty, setEditQty] = useState(loot.quantity)
  const [playerDesc, setPlayerDesc] = useState(loot.playerDescription ?? '')
  const [dmDesc, setDmDesc] = useState(loot.dmDescription ?? '')

  const handleSave = async () => {
    await onEdit(loot.id, editQty, playerDesc, dmDesc)
    setEditing(false)
  }

  return (
    <div className="px-4 py-3">
      <div className="flex items-center gap-3 mb-1">
        <span className="flex-1 text-amber-200 text-sm">{loot.name}</span>
        <span className="text-gray-500 font-mono text-xs">×{loot.quantity}</span>
        <input type="number" min={1} max={loot.quantity} value={qty}
          onChange={e => setQty(Math.min(loot.quantity, Math.max(1, Number(e.target.value))))}
          className="w-14 bg-gray-800 border border-gray-700 rounded px-2 py-1
                     text-gray-100 text-xs text-center focus:outline-none focus:border-indigo-500" />
        <Button size="sm" variant="secondary" onClick={() => onMoveToParty(loot.id, qty)}>
          → Party
        </Button>
        <Button size="sm" variant="ghost" onClick={() => setEditing(e => !e)}>
          {editing ? 'done' : 'edit'}
        </Button>
      </div>

      {loot.playerDescription && !editing && (
        <p className="text-xs text-gray-500 italic ml-0.5">{loot.playerDescription}</p>
      )}

      {editing && (
        <div className="mt-2 space-y-2 border-t border-gray-800 pt-2">
          <div className="flex gap-2 items-center">
            <label className="text-gray-500 text-xs w-12 shrink-0">Qty</label>
            <input type="number" min={0} value={editQty}
              onChange={e => setEditQty(Number(e.target.value))}
              className="w-16 bg-gray-800 border border-gray-700 rounded px-2 py-1
                         text-gray-100 text-xs text-center focus:outline-none focus:border-indigo-500" />
          </div>
          <div>
            <label className="text-gray-500 text-xs block mb-1">Player description</label>
            <textarea value={playerDesc} onChange={e => setPlayerDesc(e.target.value)} rows={2}
              className="w-full bg-gray-800 border border-gray-700 rounded px-2 py-1
                         text-gray-100 text-xs resize-none focus:outline-none focus:border-indigo-500" />
          </div>
          <div>
            <label className="text-purple-400 text-xs block mb-1">DM description</label>
            <textarea value={dmDesc} onChange={e => setDmDesc(e.target.value)} rows={2}
              className="w-full bg-gray-800 border border-purple-900 rounded px-2 py-1
                         text-gray-100 text-xs resize-none focus:outline-none focus:border-purple-700" />
          </div>
          <Button size="sm" onClick={handleSave}>Save</Button>
        </div>
      )}
    </div>
  )
}

// ---------------------------------------------------------
// Party Item Row with DM edit
// ---------------------------------------------------------
function PartyItemRow({
  item, onEdit,
}: {
  item: ItemDto
  onEdit: (id: string, qty: number | undefined, playerDesc: string, dmDesc: string) => Promise<void>
}) {
  const [editing, setEditing] = useState(false)
  const [editQty, setEditQty] = useState(item.quantity)
  const [playerDesc, setPlayerDesc] = useState(item.playerDescription ?? '')
  const [dmDesc, setDmDesc] = useState(item.dmDescription ?? '')

  const handleSave = async () => {
    await onEdit(item.id, editQty, playerDesc, dmDesc)
    setEditing(false)
  }

  const label = item.displayName ?? item.name

  return (
    <div className="px-4 py-3">
      <div className="flex items-center gap-2 mb-1">
        <span className="flex-1 text-gray-200 text-sm">{label}</span>
        <span className="text-gray-500 font-mono text-xs">×{item.quantity}</span>
        <Button size="sm" variant="ghost" onClick={() => setEditing(e => !e)}>
          {editing ? 'done' : 'edit'}
        </Button>
      </div>

      {item.playerDescription && !editing && (
        <p className="text-xs text-gray-500 italic">{item.playerDescription}</p>
      )}

      {editing && (
        <div className="mt-2 space-y-2 border-t border-gray-800 pt-2">
          <div className="flex gap-2 items-center">
            <label className="text-gray-500 text-xs w-12 shrink-0">Qty</label>
            <input type="number" min={0} value={editQty}
              onChange={e => setEditQty(Number(e.target.value))}
              className="w-16 bg-gray-800 border border-gray-700 rounded px-2 py-1
                         text-gray-100 text-xs text-center focus:outline-none focus:border-indigo-500" />
          </div>
          <div>
            <label className="text-gray-500 text-xs block mb-1">Player description</label>
            <textarea value={playerDesc} onChange={e => setPlayerDesc(e.target.value)} rows={2}
              className="w-full bg-gray-800 border border-gray-700 rounded px-2 py-1
                         text-gray-100 text-xs resize-none focus:outline-none focus:border-indigo-500" />
          </div>
          <div>
            <label className="text-purple-400 text-xs block mb-1">DM description</label>
            <textarea value={dmDesc} onChange={e => setDmDesc(e.target.value)} rows={2}
              className="w-full bg-gray-800 border border-purple-900 rounded px-2 py-1
                         text-gray-100 text-xs resize-none focus:outline-none focus:border-purple-700" />
          </div>
          <Button size="sm" onClick={handleSave}>Save</Button>
        </div>
      )}
    </div>
  )
}
