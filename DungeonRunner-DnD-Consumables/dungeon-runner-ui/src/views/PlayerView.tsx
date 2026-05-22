import { useState } from 'react'
import { useGameStore } from '../store/useGameStore'
import { ItemCard } from '../components/player/ItemCard'
import { EffectsList } from '../components/player/EffectsList'
import { RestControls } from '../components/player/RestControls'
import { PartyPanel } from '../components/player/PartyPanel'
import { ItemIcon } from '../components/shared/ItemIcon'
import { groupByCategory, timeModeLabel, itemLabel } from '../utils/item'
import type { CharacterDto, ItemDto, LootItemDto } from '../types'

type Tab = 'inventory' | 'effects' | 'party'

export default function PlayerView() {
  const {
    character, turnState, party, partyRoster, userId,
    playerNotifications, dismissPlayerNotification,
  } = useGameStore()
  const [tab, setTab] = useState<Tab>('inventory')

  if (!character) {
    return (
      <div className="min-h-screen flex items-center justify-center text-gray-500 text-sm">
        Loading character...
      </div>
    )
  }

  const pinnedItems   = character.items.filter(i => i.isPinned)
  const unpinnedItems = character.items.filter(i => !i.isPinned)
  const grouped       = groupByCategory(unpinnedItems)
  const hasLoot       = (party?.lootBox.length ?? 0) > 0
  const hasPartyInv   = (party?.inventory.length ?? 0) > 0
  const otherMembers  = partyRoster.filter(p => p.userId !== userId)

  return (
    <div className="min-h-screen max-w-lg mx-auto px-4 py-4 flex flex-col gap-4">

      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-gray-100 font-semibold text-lg">{character.name}</h1>
          <p className="text-gray-500 text-xs">{character.userId}</p>
        </div>
        {turnState && (
          <div className="text-right">
            <div className="text-gray-400 text-xs">Turn {turnState.currentTurn}</div>
            <div className="text-gray-600 text-xs">{timeModeLabel(turnState.timeMode)}</div>
          </div>
        )}
      </header>

      {playerNotifications.length > 0 && (
        <div className="space-y-1">
          {playerNotifications.map((n, i) => (
            <div key={i}
              className="bg-amber-900/40 border border-amber-700/60 rounded-lg
                         px-4 py-2 flex items-center justify-between">
              <span className="text-amber-200 text-sm">⚠ {n}</span>
              <button onClick={() => dismissPlayerNotification(i)}
                className="text-amber-400 hover:text-amber-200 ml-4 text-lg leading-none">×</button>
            </div>
          ))}
        </div>
      )}

      <nav className="flex border-b border-gray-800">
        {(['inventory', 'effects', 'party'] as Tab[]).map(t => (
          <button key={t} onClick={() => setTab(t)}
            className={`px-4 py-2 text-sm font-medium capitalize transition-colors border-b-2 -mb-px ${
              tab === t
                ? 'text-indigo-400 border-indigo-500'
                : 'text-gray-500 border-transparent hover:text-gray-300'
            }`}>
            {t}
            {t === 'effects' && character.effects.length > 0 && (
              <span className="ml-1.5 bg-purple-900 text-purple-300 rounded-full text-xs px-1.5 py-0.5">
                {character.effects.length}
              </span>
            )}
            {t === 'party' && otherMembers.length > 0 && (
              <span className="ml-1.5 bg-gray-700 text-gray-400 rounded-full text-xs px-1.5 py-0.5">
                {otherMembers.length}
              </span>
            )}
          </button>
        ))}
      </nav>

      <div className="flex-1">
        {tab === 'inventory' && (
          <div className="space-y-4">

            {hasLoot && (
              <section>
                <p className="text-xs text-amber-500 uppercase tracking-wide mb-2">⚔ Loot Available</p>
                <div className="space-y-2">
                  {party!.lootBox.map(loot => <LootRow key={loot.id} loot={loot} />)}
                </div>
              </section>
            )}

            {pinnedItems.length > 0 && (
              <section>
                <p className="text-xs text-indigo-400 uppercase tracking-wide mb-2">★ Pinned</p>
                <div className="space-y-2">
                  {pinnedItems.map(item => <ItemCard key={item.id} item={item} />)}
                </div>
              </section>
            )}

            {unpinnedItems.length === 0 && pinnedItems.length === 0 ? (
              <p className="text-gray-600 text-sm text-center py-8">Your inventory is empty.</p>
            ) : (
              Object.entries(grouped).map(([category, items]) => {
                const totalQty = items.reduce((sum, i) => sum + i.quantity, 0)
                return (
                  <section key={category}>
                    <div className="flex items-center justify-between mb-2">
                      <p className="text-xs text-gray-500 uppercase tracking-wide">{category}</p>
                      <span className="text-xs text-gray-600">{totalQty} total</span>
                    </div>
                    <div className="space-y-2">
                      {items.map(item => <ItemCard key={item.id} item={item} />)}
                    </div>
                  </section>
                )
              })
            )}

            {hasPartyInv && (
              <section>
                <p className="text-xs text-gray-400 uppercase tracking-wide mb-2">Party Inventory</p>
                <PartyPanel showInventoryOnly />
              </section>
            )}

            <RestControls />
          </div>
        )}

        {tab === 'effects' && <EffectsList effects={character.effects} />}

        {tab === 'party' && (
          <div className="space-y-4">
            {otherMembers.length === 0 ? (
              <p className="text-gray-600 text-sm text-center py-8">No other players connected.</p>
            ) : (
              otherMembers.map(member => <MemberCard key={member.userId} character={member} />)
            )}
          </div>
        )}
      </div>
    </div>
  )
}

// ---------------------------------------------------------
// Loot row — player view
// ---------------------------------------------------------
function LootRow({ loot }: { loot: LootItemDto }) {
  const { claimLootToPlayer } = useGameStore()
  const [qty, setQty] = useState(1)

  return (
    <div className="bg-gray-800 rounded-lg p-3 border border-amber-900/40">
      <div className="flex items-center justify-between gap-2 mb-1">
        <div className="flex items-center gap-2">
          <ItemIcon iconUrl={loot.iconUrl} alt={loot.name} />
          <span className="text-amber-200 text-sm font-medium">{loot.name}</span>
        </div>
        <span className="text-gray-400 font-mono text-sm">×{loot.quantity}</span>
      </div>
      {loot.playerDescription && (
        <p className="text-xs text-gray-400 italic mb-2">{loot.playerDescription}</p>
      )}
      <div className="flex items-center gap-2">
        <input type="number" min={1} max={loot.quantity} value={qty}
          onChange={e => setQty(Math.min(loot.quantity, Math.max(1, Number(e.target.value))))}
          className="w-16 bg-gray-700 border border-gray-600 rounded px-2 py-1
                     text-gray-100 text-sm text-center focus:outline-none focus:border-indigo-500" />
        <button onClick={() => claimLootToPlayer(loot.id, qty)}
          className="bg-amber-700 hover:bg-amber-600 text-amber-100 text-xs font-medium
                     px-3 py-1 rounded transition-colors">
          Claim
        </button>
      </div>
    </div>
  )
}

// ---------------------------------------------------------
// Read-only party member view
// ---------------------------------------------------------
function MemberCard({ character }: { character: CharacterDto }) {
  const [expanded, setExpanded] = useState(false)
  const pinnedItems = character.items.filter(i => i.isPinned)
  const grouped     = groupByCategory(character.items.filter(i => !i.isPinned))

  return (
    <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden">
      <button onClick={() => setExpanded(e => !e)}
        className="w-full flex items-center justify-between px-4 py-3
                   hover:bg-gray-800/50 transition-colors text-left">
        <span className="text-gray-100 font-medium">{character.name}</span>
        <div className="flex items-center gap-2">
          <span className="text-gray-500 text-xs">{character.items.length} items</span>
          <span className={`text-gray-500 text-xs transition-transform ${expanded ? 'rotate-180' : ''}`}>▲</span>
        </div>
      </button>

      {expanded && (
        <div className="px-4 pb-3 border-t border-gray-800 pt-3 space-y-3">
          {character.items.length === 0 ? (
            <p className="text-gray-600 text-xs text-center py-2">Empty.</p>
          ) : (
            <>
              {pinnedItems.length > 0 && (
                <section>
                  <p className="text-xs text-indigo-400 uppercase tracking-wide mb-1">★ Pinned</p>
                  {pinnedItems.map(item => <ReadOnlyItemRow key={item.id} item={item} />)}
                </section>
              )}
              {Object.entries(grouped).map(([cat, items]) => (
                <section key={cat}>
                  <p className="text-xs text-gray-500 uppercase tracking-wide mb-1">{cat}</p>
                  {items.map(item => <ReadOnlyItemRow key={item.id} item={item} />)}
                </section>
              ))}
            </>
          )}
        </div>
      )}
    </div>
  )
}

function ReadOnlyItemRow({ item }: { item: ItemDto }) {
  const label       = itemLabel(item, item.quantity)
  const isRenewable = item.type === 'renewable'

  return (
    <div className="flex items-center justify-between py-1 border-b border-gray-800 last:border-0">
      <div className="flex items-center gap-2 min-w-0">
        <ItemIcon iconUrl={item.iconUrl} size="sm" alt={item.name} />
        <span className="text-gray-300 text-xs truncate">{label}</span>
        {item.isActive && <span className="text-amber-400 text-xs">● lit</span>}
      </div>
      <div className="text-right shrink-0 ml-2">
        <span className="text-gray-400 font-mono text-xs">
          {isRenewable && item.maxQuantity != null
            ? `${item.quantity}/${item.maxQuantity}`
            : `×${item.quantity}`}
        </span>
        {item.remainingMinutes != null && (
          <div className="text-xs text-amber-500">{item.remainingMinutes}m</div>
        )}
      </div>
    </div>
  )
}
