import { useState } from 'react'
import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'
import { Badge } from '../shared/Badge'
import { ItemIcon } from '../shared/ItemIcon'
import { itemLabel, capVerb } from '../../utils/item'

interface Props {
  showInventoryOnly?: boolean
}

export function PartyPanel({ showInventoryOnly = false }: Props) {
  const { party, claimLootToPlayer, claimLootToParty, claimFromPartyToPlayer, usePartyItem } = useGameStore()

  if (!party) return null

  return (
    <div className="space-y-3">
      {/* Loot box — only shown on the Party tab, not when embedded in the inventory view */}
      {!showInventoryOnly && party.lootBox.length > 0 && (
        <section>
          <p className="text-xs text-gray-400 uppercase tracking-wide mb-2">Loot Box</p>
          <div className="space-y-2">
            {party.lootBox.map(loot => (
              <LootRow key={loot.id}
                id={loot.id}
                name={loot.name}
                quantity={loot.quantity}
                claimable={loot.claimable}
                playerDescription={loot.playerDescription}
                iconUrl={loot.iconUrl}
                onClaim={claimLootToPlayer}
                onClaimToParty={claimLootToParty} />
            ))}
          </div>
        </section>
      )}

      {/* Party Inventory */}
      {party.inventory.length > 0 ? (
        <div className="rounded-xl border border-teal-900/60 bg-teal-950/20 overflow-hidden">
          <div className="px-3 py-2 border-b border-teal-900/40">
            <p className="text-xs text-teal-400 uppercase tracking-wide font-medium">⚔ Shared Party Inventory</p>
          </div>
          <div className="p-3 space-y-2">
            {party.inventory.map(item => {
              const label   = itemLabel(item, item.quantity)
              const hasVerb = item.verb.trim().length > 0
              const verb    = capVerb(item.verb)

              return (
                <div key={item.id} className="bg-gray-800/80 rounded-lg p-3 border border-teal-900/30">
                  <div className="flex items-center justify-between gap-2 mb-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <ItemIcon iconUrl={item.iconUrl} alt={item.name} />
                      <span className="text-teal-100 text-sm font-medium">{label}</span>
                      {item.claimable && <Badge color="green">claimable</Badge>}
                    </div>
                    <span className="text-teal-200 font-mono text-sm shrink-0">×{item.quantity}</span>
                  </div>

                  {item.playerDescription && (
                    <p className="text-xs text-gray-400 italic mb-2">{item.playerDescription}</p>
                  )}
                  {item.dmDescriptionRevealed && item.dmDescription && (
                    <p className="text-xs text-purple-300 italic mb-2">{item.dmDescription}</p>
                  )}

                  <div className="flex gap-2 flex-wrap">
                    {hasVerb && (
                      <Button size="sm" variant="secondary"
                        onClick={() => usePartyItem(item.id)}
                        disabled={item.quantity <= 0}>
                        {verb}
                      </Button>
                    )}
                    {item.claimable && (
                      <ClaimFromPartyButton itemId={item.id} max={item.quantity}
                        onClaim={claimFromPartyToPlayer} />
                    )}
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      ) : !showInventoryOnly ? (
        <p className="text-gray-600 text-sm text-center py-6">Party inventory is empty.</p>
      ) : null}
    </div>
  )
}

// ---------------------------------------------------------
// Loot row — claim either to self (claimable) or to party (non-claimable)
// ---------------------------------------------------------
function LootRow({
  id, name, quantity, claimable, playerDescription, iconUrl, onClaim, onClaimToParty,
}: {
  id: string; name: string; quantity: number; claimable: boolean
  playerDescription?: string
  iconUrl?: string
  onClaim: (lootItemId: string, quantity: number) => Promise<void>
  onClaimToParty: (lootItemId: string, quantity: number) => Promise<void>
}) {
  const [claiming, setClaiming] = useState(1)

  return (
    <div className="bg-gray-800 rounded-lg p-3 border border-amber-900/40">
      <div className="flex items-center justify-between gap-2 mb-1">
        <div className="flex items-center gap-2">
          <ItemIcon iconUrl={iconUrl} alt={name} />
          <span className="text-amber-200 text-sm font-medium">{name}</span>
          {!claimable && <Badge color="gray">party only</Badge>}
        </div>
        <span className="text-gray-400 font-mono text-sm">×{quantity}</span>
      </div>
      {playerDescription && (
        <p className="text-xs text-gray-400 italic mb-2">{playerDescription}</p>
      )}
      <div className="flex items-center gap-2">
        <input type="number" min={1} max={quantity} value={claiming}
          onChange={e => setClaiming(Math.min(quantity, Math.max(1, Number(e.target.value))))}
          className="w-16 bg-gray-700 border border-gray-600 rounded px-2 py-1
                     text-gray-100 text-sm text-center focus:outline-none focus:border-indigo-500" />
        {claimable ? (
          <Button size="sm" onClick={() => onClaim(id, claiming)} disabled={quantity <= 0}>Claim</Button>
        ) : (
          <Button size="sm" variant="secondary"
            onClick={() => onClaimToParty(id, claiming)} disabled={quantity <= 0}>
            → Party Inventory
          </Button>
        )}
      </div>
    </div>
  )
}

function ClaimFromPartyButton({
  itemId, max, onClaim,
}: {
  itemId: string; max: number
  onClaim: (itemId: string, quantity: number) => Promise<void>
}) {
  const [qty, setQty] = useState(1)

  return (
    <div className="flex items-center gap-1">
      <input type="number" min={1} max={max} value={qty}
        onChange={e => setQty(Math.min(max, Math.max(1, Number(e.target.value))))}
        className="w-14 bg-gray-700 border border-gray-600 rounded px-1.5 py-1
                   text-gray-100 text-sm text-center focus:outline-none focus:border-indigo-500" />
      <Button size="sm" variant="ghost" onClick={() => onClaim(itemId, qty)}>Take</Button>
    </div>
  )
}
