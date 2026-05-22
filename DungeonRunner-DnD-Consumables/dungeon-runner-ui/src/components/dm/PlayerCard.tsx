import { useState } from 'react'
import type { CharacterDto, ItemDto } from '../../types'
import { useGameStore, DMAdjustFields } from '../../store/useGameStore'
import { Button } from '../shared/Button'
import { Badge } from '../shared/Badge'
import { ItemIcon } from '../shared/ItemIcon'
import { CatalogPickerModal } from '../shared/CatalogPickerModal'
import { itemLabel, isItemCritical } from '../../utils/item'

interface Props { character: CharacterDto }

export function PlayerCard({ character }: Props) {
  const [expanded, setExpanded] = useState(true)
  const { dmDeleteItem, dmAdjustItem, dmGiveItem, dmRest, catalog } = useGameStore()
  const [showGiveModal, setShowGiveModal] = useState(false)

  const hasCriticalItem = character.items.some(isItemCritical)

  return (
    <>
      <div className={`bg-gray-900 rounded-xl overflow-hidden border ${
        hasCriticalItem ? 'border-red-900/60' : 'border-gray-800'
      }`}>
        <button onClick={() => setExpanded(e => !e)}
          className="w-full flex items-center justify-between px-4 py-3
                     hover:bg-gray-800/50 transition-colors text-left">
          <div>
            <span className="text-gray-100 font-medium">{character.name}</span>
            <span className="text-gray-600 text-xs ml-2">{character.userId}</span>
          </div>
          <div className="flex items-center gap-3">
            {character.effects.length > 0 && (
              <Badge color="purple">{character.effects.length} effect{character.effects.length !== 1 ? 's' : ''}</Badge>
            )}
            {hasCriticalItem && <Badge color="red">critical</Badge>}
            <span className="text-gray-500 text-xs">{character.items.length} items</span>
            <span className={`text-gray-500 text-xs transition-transform ${expanded ? 'rotate-180' : ''}`}>▲</span>
          </div>
        </button>

        {expanded && (
          <div className="flex border-t border-gray-800">
            <div className="flex-1 px-4 py-3 space-y-1.5 min-w-0">
              {character.items.length === 0 ? (
                <p className="text-gray-600 text-xs text-center py-3">No items.</p>
              ) : (
                character.items.map(item => (
                  <DmItemRow key={item.id} item={item} userId={character.userId}
                    onDelete={dmDeleteItem} onAdjust={dmAdjustItem} />
                ))
              )}
            </div>

            <div className="w-36 shrink-0 border-l border-gray-800 px-3 py-3 flex flex-col gap-2">
              <p className="text-gray-600 text-xs uppercase tracking-wide mb-1">Controls</p>
              <Button size="sm" variant="ghost" className="w-full text-left"
                onClick={() => dmRest(character.userId, 'short')}>Short Rest</Button>
              <Button size="sm" variant="ghost" className="w-full text-left"
                onClick={() => dmRest(character.userId, 'long')}>Long Rest</Button>
              <Button size="sm" variant="secondary" className="w-full text-left"
                onClick={() => setShowGiveModal(true)} disabled={catalog.length === 0}>
                Give Item
              </Button>
            </div>
          </div>
        )}
      </div>

      {showGiveModal && (
        <CatalogPickerModal catalog={catalog}
          title={`Give Item to ${character.name}`}
          actionLabel="Give"
          onPick={(templateId, qty) => {
            dmGiveItem(character.userId, templateId, qty)
            setShowGiveModal(false)
          }}
          onClose={() => setShowGiveModal(false)} />
      )}
    </>
  )
}

// ---------------------------------------------------------
// Item row in DM view — now shows icon + scenario badge, editor has scenario tag
// ---------------------------------------------------------
function DmItemRow({
  item, userId, onDelete, onAdjust,
}: {
  item: ItemDto; userId: string
  onDelete: (userId: string, itemId: string) => Promise<void>
  onAdjust: (userId: string, itemId: string, fields: DMAdjustFields) => Promise<void>
}) {
  const scenarios = useGameStore(s => s.scenarios)
  const [editing, setEditing] = useState(false)
  const [qty,        setQty]        = useState(item.quantity)
  const [mins,       setMins]       = useState(item.remainingMinutes ?? 0)
  const [maxQty,     setMaxQty]     = useState(item.maxQuantity ?? 0)
  const [playerDesc, setPlayerDesc] = useState(item.playerDescription ?? '')
  const [dmDesc,     setDmDesc]     = useState(item.dmDescription ?? '')
  const [scenario,   setScenario]   = useState(item.scenario ?? '')

  const label       = itemLabel(item, item.quantity)
  const scenarioLabel = item.scenario
    ? (scenarios.find(s => s.id === item.scenario)?.name ?? item.scenario)
    : null
  const isTimed     = item.type === 'equipment' || item.maxMinutes != null
  const isRenewable = item.type === 'renewable'

  const currentCharges = item.charges ?? item.maxQuantity ?? 0
  const maxCharges     = item.maxQuantity ?? 0
  const chargePct = isRenewable && maxCharges > 0
    ? Math.round((currentCharges / maxCharges) * 100) : null

  const durationPct = (isTimed && item.isActive &&
    item.remainingMinutes != null && item.maxMinutes != null && item.maxMinutes > 0)
    ? Math.round((item.remainingMinutes / item.maxMinutes) * 100) : null

  const isCritical = (durationPct != null && durationPct <= 25) ||
                     (chargePct   != null && chargePct   <= 25)

  const handleSave = () => {
    const nextScenario = scenario.trim() || null
    const scenarioChanged = (item.scenario ?? null) !== nextScenario
    onAdjust(userId, item.id, {
      quantity: qty,
      remainingMinutes: isTimed ? mins : undefined,
      maxQuantity: isRenewable ? maxQty : undefined,
      playerDescription: playerDesc,
      dmDescription: dmDesc,
      ...(scenarioChanged ? { scenario: nextScenario, updateScenario: true } : {}),
    })
    setEditing(false)
  }

  return (
    <div className={`rounded-lg p-2 border ${
      isCritical ? 'bg-red-950/30 border-red-900/50' : 'bg-gray-800 border-gray-700'
    }`}>
      <div className="flex items-center gap-2 justify-between">
        <div className="flex items-center gap-1.5 flex-1 min-w-0">
          <ItemIcon iconUrl={item.iconUrl} size="sm" alt={item.name} />
          <span className="text-gray-200 text-xs font-medium truncate">{label}</span>
          {item.isActive && <span className="text-amber-400 text-xs">●</span>}
          {item.isPinned && <span className="text-indigo-400 text-xs">★</span>}
          {scenarioLabel && <Badge color="purple">⚡ {scenarioLabel}</Badge>}
        </div>
        <div className="flex items-center gap-1.5 shrink-0">
          <span className="text-gray-400 font-mono text-xs">
            {isRenewable && maxCharges > 0
              ? `${currentCharges}/${maxCharges} uses`
              : `×${item.quantity}`}
          </span>
          <Button size="sm" variant="ghost" onClick={() => setEditing(e => !e)}>
            {editing ? 'done' : 'edit'}
          </Button>
          <Button size="sm" variant="danger"
            onClick={() => { if (confirm(`Remove ${label}?`)) onDelete(userId, item.id) }}>
            ×
          </Button>
        </div>
      </div>

      {chargePct != null && (
        <div className="mt-1">
          <div className="flex justify-between items-center mb-0.5">
            <span className="text-xs text-purple-400">{currentCharges} / {maxCharges} uses</span>
            <span className="text-xs text-purple-600">{chargePct}%</span>
          </div>
          <div className="h-1 bg-gray-700 rounded-full overflow-hidden">
            <div className={`h-full rounded-full ${
              chargePct > 50 ? 'bg-violet-500' : chargePct > 25 ? 'bg-purple-500' : 'bg-purple-700'
            }`} style={{ width: `${chargePct}%` }} />
          </div>
        </div>
      )}

      {durationPct != null && (
        <div className="mt-1 h-1 bg-gray-700 rounded-full overflow-hidden">
          <div className={`h-full rounded-full ${
            durationPct > 50 ? 'bg-amber-500' : durationPct > 25 ? 'bg-orange-500' : 'bg-red-500'
          }`} style={{ width: `${durationPct}%` }} />
        </div>
      )}

      {isTimed && item.remainingMinutes != null && (
        <div className={`text-xs mt-0.5 ${isCritical ? 'text-red-400' : 'text-amber-500'}`}>
          {item.remainingMinutes} min{item.maxMinutes != null ? ` / ${item.maxMinutes}` : ''}
        </div>
      )}

      {item.playerDescription && (
        <p className="text-xs text-gray-400 italic mt-0.5 truncate">{item.playerDescription}</p>
      )}

      {item.dmDescription && (
        <div className="mt-1 flex items-center gap-2">
          <div onClick={() => onAdjust(userId, item.id, { dmDescriptionRevealed: !item.dmDescriptionRevealed })}
            className={`relative w-7 h-4 rounded-full transition-colors cursor-pointer ${
              item.dmDescriptionRevealed ? 'bg-purple-600' : 'bg-gray-700'
            }`}>
            <div className={`absolute top-0.5 left-0.5 w-3 h-3 rounded-full bg-white
                            transition-transform ${item.dmDescriptionRevealed ? 'translate-x-3' : ''}`} />
          </div>
          <span className={`text-xs ${item.dmDescriptionRevealed ? 'text-purple-300' : 'text-gray-500'}`}>
            {item.dmDescriptionRevealed ? 'Revealed' : 'Reveal'}
          </span>
          <span className="text-xs text-purple-400 italic truncate flex-1">{item.dmDescription}</span>
        </div>
      )}

      {editing && (
        <div className="mt-2 space-y-2 border-t border-gray-700 pt-2">
          <div className="flex flex-wrap gap-2 items-end">
            <AdjField label="Qty"  value={qty}    onChange={setQty} />
            {isRenewable && <AdjField label="Max"  value={maxQty} onChange={setMaxQty} />}
            {isTimed     && <AdjField label="Mins" value={mins}   onChange={setMins} />}
          </div>
          <div>
            <label className="text-gray-500 text-xs block mb-1">Scenario tag</label>
            <select value={scenario} onChange={e => setScenario(e.target.value)}
              className="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1
                         text-gray-100 text-xs focus:outline-none focus:border-indigo-500">
              <option value="">(untagged)</option>
              {scenarios.map(s => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
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
          <Button size="sm" onClick={handleSave}>Save</Button>
        </div>
      )}
    </div>
  )
}

function AdjField({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <div>
      <p className="text-gray-500 text-xs mb-0.5">{label}</p>
      <input type="number" min={0} value={value} onChange={e => onChange(Number(e.target.value))}
        className="w-16 bg-gray-700 border border-gray-600 rounded px-2 py-1
                   text-gray-100 text-xs text-center focus:outline-none focus:border-indigo-500" />
    </div>
  )
}
