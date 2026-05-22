import { useState } from 'react'
import type { EncounterDto, LootItemDto } from '../../types'
import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'
import { Badge } from '../shared/Badge'
import { ItemIcon } from '../shared/ItemIcon'
import { CatalogPickerModal } from '../shared/CatalogPickerModal'

export function EncounterPanel() {
  const {
    encounters, catalog, scenarios,
    dmCreateEncounter, dmAddItemToEncounter, dmAddCatalogItemToEncounter,
    dmEditEncounterItem, dmRemoveItemFromEncounter,
    dmPushEncounterToLootBox, dmPushSingleEncounterItem, dmDeleteEncounter,
  } = useGameStore()

  const [newName, setNewName] = useState('')
  const [newScenario, setNewScenario] = useState('')
  const [showScenarioInput, setShowScenarioInput] = useState(false)

  const handleCreate = () => {
    if (!newName.trim()) return
    dmCreateEncounter(newName.trim(), newScenario.trim() || undefined)
    setNewName(''); setNewScenario(''); setShowScenarioInput(false)
  }

  return (
    <div className="space-y-4">
      <div className="bg-gray-900 border border-gray-800 rounded-xl p-4">
        <div className="flex items-center justify-between mb-3">
          <p className="text-gray-400 text-xs uppercase tracking-wide">New Encounter</p>
          {scenarios.length > 0 && (
            <button onClick={() => setShowScenarioInput(v => !v)}
              className="text-xs text-gray-500 hover:text-gray-300">
              {showScenarioInput ? 'hide scenario' : '+ scenario'}
            </button>
          )}
        </div>
        <div className="flex gap-2">
          <input type="text" placeholder="e.g. Goblin Ambush, Dragon's Hoard..."
            value={newName} onChange={e => setNewName(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleCreate()}
            className="flex-1 bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                       text-gray-100 text-sm placeholder-gray-600
                       focus:outline-none focus:border-indigo-500" />
          <Button onClick={handleCreate} disabled={!newName.trim()}>Add</Button>
        </div>
        {showScenarioInput && scenarios.length > 0 && (
          <select value={newScenario} onChange={e => setNewScenario(e.target.value)}
            className="mt-2 w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                       text-gray-100 text-xs focus:outline-none focus:border-indigo-500">
            <option value="">(no scenario — normal world)</option>
            {scenarios.map(s => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        )}
      </div>

      {encounters.length === 0 ? (
        <p className="text-gray-600 text-sm text-center py-8">
          No encounters staged. Create one above to pre-fill loot for upcoming fights.
        </p>
      ) : (
        encounters.map(enc => (
          <EncounterCard key={enc.id} encounter={enc} catalog={catalog} scenarios={scenarios}
            onAddItem={dmAddItemToEncounter}
            onAddCatalogItem={dmAddCatalogItemToEncounter}
            onEditItem={dmEditEncounterItem}
            onRemoveItem={dmRemoveItemFromEncounter}
            onPushAll={dmPushEncounterToLootBox}
            onPushSingle={dmPushSingleEncounterItem}
            onDelete={dmDeleteEncounter} />
        ))
      )}
    </div>
  )
}

function EncounterCard({
  encounter, catalog, scenarios,
  onAddItem, onAddCatalogItem, onEditItem, onRemoveItem,
  onPushAll, onPushSingle, onDelete,
}: {
  encounter: EncounterDto
  catalog: import('../../types').ItemTemplateDto[]
  scenarios: import('../../types').ScenarioDto[]
  onAddItem: (encId: string, name: string, qty: number) => Promise<void>
  onAddCatalogItem: (encId: string, templateId: string, qty: number) => Promise<void>
  onEditItem: (encId: string, lootItemId: string, qty: number | undefined, playerDesc: string, dmDesc: string) => Promise<void>
  onRemoveItem: (encId: string, lootItemId: string) => Promise<void>
  onPushAll: (encId: string) => Promise<void>
  onPushSingle: (encId: string, lootItemId: string) => Promise<void>
  onDelete: (encId: string) => Promise<void>
}) {
  const [expanded,      setExpanded]      = useState(true)
  const [customName,    setCustomName]    = useState('')
  const [customQty,     setCustomQty]     = useState(1)
  const [showCatalogModal, setShowCatalogModal] = useState(false)

  const totalItems = encounter.items.reduce((s, i) => s + i.quantity, 0)
  const encounterScenarioLabel = encounter.scenario
    ? (scenarios.find(s => s.id === encounter.scenario)?.name ?? encounter.scenario)
    : null

  return (
    <>
      <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
        <div className="flex items-center justify-between px-4 py-3 border-b border-gray-800">
          <button onClick={() => setExpanded(e => !e)}
            className="flex items-center gap-3 flex-1 text-left hover:text-gray-200 transition-colors">
            <span className="text-gray-100 font-medium">{encounter.name}</span>
            {encounterScenarioLabel && <Badge color="purple">⚡ {encounterScenarioLabel}</Badge>}
            <span className="text-gray-600 text-xs">
              {encounter.items.length} type{encounter.items.length !== 1 ? 's' : ''} · {totalItems} item{totalItems !== 1 ? 's' : ''}
            </span>
            <span className={`text-gray-500 text-xs ml-auto transition-transform ${expanded ? 'rotate-180' : ''}`}>▲</span>
          </button>
          <div className="flex gap-2 ml-3 shrink-0">
            <Button size="sm"
              onClick={() => {
                if (confirm(`Move all loot from "${encounter.name}" to the loot box?`))
                  onPushAll(encounter.id)
              }}
              disabled={encounter.items.length === 0}>
              → All to Loot Box
            </Button>
            <Button size="sm" variant="danger"
              onClick={() => {
                if (confirm(`Delete encounter "${encounter.name}"?`)) onDelete(encounter.id)
              }}>
              ×
            </Button>
          </div>
        </div>

        {expanded && (
          <div className="px-4 py-3 space-y-3">
            <div className="flex gap-2">
              <input type="text" placeholder="Custom item name..." value={customName}
                onChange={e => setCustomName(e.target.value)}
                onKeyDown={e => {
                  if (e.key === 'Enter' && customName.trim()) {
                    onAddItem(encounter.id, customName.trim(), customQty)
                    setCustomName(''); setCustomQty(1)
                  }
                }}
                className="flex-1 bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                           text-gray-100 text-sm placeholder-gray-600
                           focus:outline-none focus:border-indigo-500" />
              <input type="number" min={1} value={customQty}
                onChange={e => setCustomQty(Math.max(1, Number(e.target.value)))}
                className="w-14 bg-gray-800 border border-gray-700 rounded px-2 py-1.5
                           text-gray-100 text-sm text-center focus:outline-none focus:border-indigo-500" />
              <Button size="sm" disabled={!customName.trim()}
                onClick={() => {
                  onAddItem(encounter.id, customName.trim(), customQty)
                  setCustomName(''); setCustomQty(1)
                }}>
                Add
              </Button>
              {catalog.length > 0 && (
                <Button size="sm" variant="secondary" onClick={() => setShowCatalogModal(true)}>
                  From Catalog
                </Button>
              )}
            </div>

            {encounter.items.length === 0 ? (
              <p className="text-gray-600 text-xs text-center py-2">No items staged yet.</p>
            ) : (
              <div className="space-y-1.5">
                {encounter.items.map(item => (
                  <EncounterItemRow key={item.id} encounterId={encounter.id} item={item}
                    scenarios={scenarios}
                    onEdit={onEditItem} onRemove={onRemoveItem} onPushSingle={onPushSingle} />
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {showCatalogModal && (
        <CatalogPickerModal catalog={catalog}
          title="Add from Catalog"
          subtitle={`To encounter: ${encounter.name}`}
          actionLabel="Add to Encounter"
          onPick={(templateId, qty) => {
            onAddCatalogItem(encounter.id, templateId, qty)
            setShowCatalogModal(false)
          }}
          onClose={() => setShowCatalogModal(false)} />
      )}
    </>
  )
}

function EncounterItemRow({
  encounterId, item, scenarios, onEdit, onRemove, onPushSingle,
}: {
  encounterId: string; item: LootItemDto
  scenarios: import('../../types').ScenarioDto[]
  onEdit: (encId: string, lootItemId: string, qty: number | undefined, playerDesc: string, dmDesc: string) => Promise<void>
  onRemove: (encId: string, lootItemId: string) => Promise<void>
  onPushSingle: (encId: string, lootItemId: string) => Promise<void>
}) {
  const [editing,    setEditing]    = useState(false)
  const [editQty,    setEditQty]    = useState(item.quantity)
  const [playerDesc, setPlayerDesc] = useState(item.playerDescription ?? '')
  const [dmDesc,     setDmDesc]     = useState(item.dmDescription ?? '')
  const scenarioLabel = item.scenario
    ? (scenarios.find(s => s.id === item.scenario)?.name ?? item.scenario)
    : null

  return (
    <div className="bg-gray-800 rounded-lg border border-gray-700">
      <div className="flex items-center gap-2 px-3 py-2">
        <ItemIcon iconUrl={item.iconUrl} alt={item.name} />
        <span className="flex-1 text-amber-200 text-sm truncate">{item.name}</span>
        {scenarioLabel && <Badge color="purple">⚡ {scenarioLabel}</Badge>}
        <span className="text-gray-500 font-mono text-xs">×{item.quantity}</span>
        <Button size="sm" variant="secondary"
          onClick={() => {
            if (confirm(`Move ${item.name} to the loot box?`))
              onPushSingle(encounterId, item.id)
          }}>
          → Loot Box
        </Button>
        <Button size="sm" variant="ghost" onClick={() => setEditing(e => !e)}>{editing ? 'done' : 'edit'}</Button>
        <button onClick={() => onRemove(encounterId, item.id)}
          className="text-gray-600 hover:text-red-400 transition-colors text-xs px-1">×</button>
      </div>

      {item.playerDescription && !editing && (
        <p className="text-xs text-gray-500 italic px-3 pb-2">{item.playerDescription}</p>
      )}

      {editing && (
        <div className="px-3 pb-3 space-y-2 border-t border-gray-700 pt-2">
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
          <Button size="sm"
            onClick={() => { onEdit(encounterId, item.id, editQty, playerDesc, dmDesc); setEditing(false) }}>
            Save
          </Button>
        </div>
      )}
    </div>
  )
}
