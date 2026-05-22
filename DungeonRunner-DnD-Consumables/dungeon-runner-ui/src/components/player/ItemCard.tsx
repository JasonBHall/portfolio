import { useState } from 'react'
import type { ItemDto } from '../../types'
import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'
import { Badge } from '../shared/Badge'
import { ItemIcon } from '../shared/ItemIcon'
import { itemLabel, capVerb } from '../../utils/item'

interface Props { item: ItemDto }

export function ItemCard({ item }: Props) {
  const {
    useItem, recoverItem, lightItem, snuffItem, spendRenewable,
    pinItem, giveItemToParty, giveItemToPlayer, fuelItem,
    partyRoster, userId, character,
  } = useGameStore()

  const [showGive,   setShowGive]   = useState(false)
  const [giveQty,    setGiveQty]    = useState(1)
  const [giveTo,     setGiveTo]     = useState<string>('party')
  const [showFuel,   setShowFuel]   = useState(false)
  const [fuelItemId, setFuelItemId] = useState('')
  const [fuelQty,    setFuelQty]    = useState(1)

  const isEquipment  = item.type === 'equipment'
  const isRenewable  = item.type === 'renewable'
  const isConsumable = item.type === 'consumable'

  const label   = itemLabel(item, item.quantity)
  const hasVerb = item.verb.trim().length > 0
  const verb    = capVerb(item.verb)
  const verbOff = item.verbOff ? capVerb(item.verbOff) : 'Snuff'

  const otherPlayers = partyRoster.filter(p => p.userId !== userId)

  const fuelItems = item.acceptedFuelNames.length > 0
    ? (character?.items ?? []).filter(i =>
        i.id !== item.id &&
        item.acceptedFuelNames.some(accepted =>
          i.name.toLowerCase() === accepted.toLowerCase() ||
          (i.displayName?.toLowerCase() === accepted.toLowerCase())
        )
      )
    : []

  // ── Duration burndown ──────────────────────────────────────────────────
  const maxDuration  = item.maxFuelMinutes ?? item.maxMinutes
  const hasDuration  = maxDuration != null && maxDuration > 0
  const hasRemaining = item.remainingMinutes != null

  let durationPct: number | null = null
  if (hasDuration && hasRemaining)
    durationPct = Math.max(0, Math.min(100, Math.round((item.remainingMinutes! / maxDuration!) * 100)))
  else if (isEquipment && hasRemaining && item.remainingMinutes! <= 0)
    durationPct = 0

  const showDurationBar =
    durationPct != null &&
    (isEquipment || item.isActive || (item.isReusable && (item.remainingMinutes ?? 0) > 0))

  const durationBarColor =
    durationPct == null ? '' :
    durationPct > 50    ? 'bg-amber-500' :
    durationPct > 25    ? 'bg-orange-500' :
                          'bg-red-500'

  // ── Charges burndown (renewable) ───────────────────────────────────────
  const currentCharges = item.charges ?? item.maxQuantity ?? 0
  const maxCharges     = item.maxQuantity ?? 0
  const chargePct      = isRenewable && maxCharges > 0
    ? Math.max(0, Math.min(100, Math.round((currentCharges / maxCharges) * 100)))
    : null

  const chargeBarColor =
    chargePct == null ? '' :
    chargePct > 50    ? 'bg-violet-500' :
    chargePct > 25    ? 'bg-purple-500' :
                        'bg-purple-700'

  const handleGive = async () => {
    if (giveTo === 'party') await giveItemToParty(item.id, giveQty)
    else await giveItemToPlayer(giveTo, item.id, giveQty)
    setShowGive(false); setGiveQty(1)
  }

  const handleFuel = async () => {
    if (!fuelItemId) return
    await fuelItem(item.id, fuelItemId, fuelQty)
    setShowFuel(false); setFuelQty(1); setFuelItemId('')
  }

  const primaryButton = hasVerb
    ? (hasDuration || isEquipment)
      ? item.isActive
        ? <Button size="sm" variant="secondary" className="w-full" onClick={() => snuffItem(item.id)}>{verbOff}</Button>
        : <Button size="sm" variant="secondary" className="w-full" onClick={() => lightItem(item.id)}
            disabled={
              isConsumable && !item.isReusable ? item.quantity <= 0 :
              isConsumable && item.isReusable  ? item.quantity <= 0 && (item.remainingMinutes ?? 0) <= 0 :
              false
            }>{verb}</Button>
      : isRenewable
        ? <Button size="sm" className="w-full" onClick={() => spendRenewable(item.id)} disabled={currentCharges <= 0}>{verb}</Button>
        : <Button size="sm" className="w-full" onClick={() => useItem(item.id)} disabled={item.quantity <= 0}>{verb}</Button>
    : null

  const hasSecondaryActions =
    (isConsumable && item.recoveryChance != null && item.recoveryChance > 0) ||
    (item.acceptedFuelNames.length > 0 && fuelItems.length > 0) ||
    item.canGive

  return (
    <div className={`bg-gray-800 rounded-lg border ${
      item.isPinned ? 'border-indigo-600' : 'border-gray-700'
    }`}>
      <div className="flex gap-2 p-3">
        <div className="flex-1 min-w-0">
          {/* Name row — icon sits inline with the name, collapses when absent */}
          <div className="flex items-center gap-1.5 mb-1 flex-wrap">
            <button onClick={() => pinItem(item.id, !item.isPinned)}
              className={`text-sm leading-none transition-colors shrink-0 ${
                item.isPinned ? 'text-indigo-400' : 'text-gray-600 hover:text-gray-400'
              }`}>
              {item.isPinned ? '★' : '☆'}
            </button>

            <ItemIcon iconUrl={item.iconUrl} alt={item.name} />

            <span className="text-gray-100 font-medium text-sm truncate">{label}</span>

            {item.quantity > 1 && (
              <span className="text-gray-400 font-mono text-xs shrink-0">×{item.quantity}</span>
            )}

            {item.isActive && <span className="text-xs text-amber-400 font-medium shrink-0">● lit</span>}
            {isEquipment && !item.isActive && hasRemaining && item.remainingMinutes! > 0 && (
              <span className="text-xs text-gray-500 shrink-0">(off)</span>
            )}
            {isEquipment && hasRemaining && item.remainingMinutes! <= 0 && (
              <span className="text-xs text-red-400 shrink-0">needs fuel</span>
            )}
            {item.dmDescriptionRevealed && item.dmDescription && (
              <Badge color="purple">identified</Badge>
            )}
          </div>

          {chargePct != null && (
            <div className="mb-1.5">
              <div className="flex justify-between items-center mb-0.5">
                <span className="text-xs text-purple-400">{currentCharges} / {maxCharges} uses</span>
                <span className="text-xs text-purple-600">{chargePct}%</span>
              </div>
              <div className="h-1.5 bg-gray-700 rounded-full overflow-hidden">
                <div className={`h-full rounded-full transition-all ${chargeBarColor}`}
                  style={{ width: `${chargePct}%` }} />
              </div>
            </div>
          )}

          {showDurationBar && (
            <div className="mb-1.5">
              <div className="flex justify-between items-center mb-0.5">
                <span className="text-xs text-gray-500">
                  {item.remainingMinutes} min
                  {item.isReusable && !item.isActive && !isEquipment && ' (paused)'}
                </span>
                <span className="text-xs text-gray-600">
                  {item.maxFuelMinutes != null ? `max ${item.maxFuelMinutes}` : maxDuration != null ? `/ ${maxDuration} min` : ''}{' '}
                  {durationPct}%
                </span>
              </div>
              <div className="h-1.5 bg-gray-700 rounded-full overflow-hidden">
                <div className={`h-full rounded-full transition-all ${durationBarColor}`}
                  style={{ width: `${durationPct}%` }} />
              </div>
            </div>
          )}

          {item.playerDescription && (
            <p className="text-xs text-gray-400 italic leading-tight">{item.playerDescription}</p>
          )}
          {item.dmDescriptionRevealed && item.dmDescription && (
            <p className="text-xs text-purple-300 italic leading-tight mt-0.5">{item.dmDescription}</p>
          )}
          {item.recoveryChance != null && item.recoveryChance > 0 && (
            <p className="text-xs text-gray-500 mt-0.5">{Math.round(item.recoveryChance * 100)}% recovery</p>
          )}
        </div>

        {(primaryButton != null || hasSecondaryActions) && (
          <div className="w-16 shrink-0 flex flex-col gap-2.5 justify-start
                          border-l border-gray-700/50 pl-2.5">
            {primaryButton}

            {isConsumable && item.recoveryChance != null && item.recoveryChance > 0 && (
              <button onClick={() => recoverItem(item.id)}
                className="w-full text-center text-xs text-gray-400 hover:text-gray-200
                           py-1 rounded border border-gray-700 hover:border-gray-600 transition-colors">
                Recover?
              </button>
            )}

            {item.acceptedFuelNames.length > 0 && fuelItems.length > 0 && (
              <button onClick={() => { setShowFuel(v => !v); setShowGive(false) }}
                className={`w-full text-center text-xs py-1 rounded border transition-colors ${
                  showFuel
                    ? 'text-amber-300 border-amber-700 bg-amber-900/20'
                    : 'text-gray-400 border-gray-700 hover:border-gray-600 hover:text-gray-200'
                }`}>
                Fuel
              </button>
            )}

            {item.canGive && (
              <button onClick={() => { setShowGive(v => !v); setShowFuel(false) }}
                className={`w-full text-center text-xs py-1 rounded border transition-colors ${
                  showGive
                    ? 'text-indigo-300 border-indigo-700 bg-indigo-900/20'
                    : 'text-gray-400 border-gray-700 hover:border-gray-600 hover:text-gray-200'
                }`}>
                Give
              </button>
            )}
          </div>
        )}
      </div>

      {showFuel && (
        <div className="border-t border-gray-700 px-3 py-2 space-y-2">
          <p className="text-xs text-amber-400">
            +{item.minutesPerFuelUnit} min per unit
            {item.maxFuelMinutes != null && ` · max ${item.maxFuelMinutes} min`}
          </p>
          <div className="flex gap-2 items-center">
            <select value={fuelItemId} onChange={e => setFuelItemId(e.target.value)}
              className="flex-1 bg-gray-700 border border-gray-600 rounded px-2 py-1.5
                         text-gray-100 text-xs focus:outline-none focus:border-amber-600">
              <option value="">Select fuel...</option>
              {fuelItems.map(fi => (
                <option key={fi.id} value={fi.id}>{fi.displayName ?? fi.name} (×{fi.quantity})</option>
              ))}
            </select>
            <input type="number" min={1}
              max={fuelItems.find(fi => fi.id === fuelItemId)?.quantity ?? 1}
              value={fuelQty}
              onChange={e => setFuelQty(Math.max(1, Number(e.target.value)))}
              className="w-12 bg-gray-700 border border-gray-600 rounded px-2 py-1.5
                         text-gray-100 text-xs text-center focus:outline-none focus:border-amber-600" />
            <Button size="sm" onClick={handleFuel} disabled={!fuelItemId}>Add</Button>
          </div>
        </div>
      )}

      {showGive && item.canGive && (
        <div className="border-t border-gray-700 px-3 py-2">
          <div className="flex gap-2 items-center">
            <select value={giveTo} onChange={e => setGiveTo(e.target.value)}
              className="flex-1 bg-gray-700 border border-gray-600 rounded px-2 py-1.5
                         text-gray-100 text-xs focus:outline-none focus:border-indigo-500">
              <option value="party">Party Inventory</option>
              {otherPlayers.map(p => <option key={p.userId} value={p.userId}>{p.name}</option>)}
            </select>
            {!isEquipment && (
              <input type="number" min={1} max={item.quantity} value={giveQty}
                onChange={e => setGiveQty(Math.min(item.quantity, Math.max(1, Number(e.target.value))))}
                className="w-12 bg-gray-700 border border-gray-600 rounded px-2 py-1.5
                           text-gray-100 text-xs text-center focus:outline-none focus:border-indigo-500" />
            )}
            <Button size="sm" onClick={handleGive}
              disabled={!isEquipment && item.quantity <= 0}>
              Give
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
