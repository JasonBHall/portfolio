import { useMemo, useState } from 'react'
import type { ItemTemplateDto } from '../../types'
import { Button } from './Button'
import { ItemIcon } from './ItemIcon'
import { templateLabel } from '../../utils/item'

interface Props {
  catalog: ItemTemplateDto[]
  /** Shown as the modal heading. */
  title: string
  /** Label for the primary action button (e.g. "Give", "Drop into Loot Box"). */
  actionLabel: string
  /** Optional rail above the item list. Use for per-context metadata like a character name. */
  subtitle?: string
  /** Hides the quantity field — useful if the caller only wants a 1-unit drop. */
  hideQuantity?: boolean
  /** Tailwind classes used when an item is selected. Defaults to indigo. */
  accentSelectedClass?: string
  /**
   * Filter the incoming catalog before any search/category filtering is applied.
   * Used e.g. to hide scenario-tagged items when normal play is active.
   */
  preFilter?: (t: ItemTemplateDto) => boolean
  onPick: (templateId: string, quantity: number) => void
  onClose: () => void
}

/**
 * One modal to rule them all. Previously there were four copies of this —
 * GiveItemModal (PlayerCard), GiveToPartyModal (PartyInventoryCard),
 * GiveToLootBoxModal (LootBoxCard), and CatalogPickerModal (EncounterPanel),
 * each ~100 lines of near-identical code.
 */
export function CatalogPickerModal({
  catalog,
  title,
  actionLabel,
  subtitle,
  hideQuantity = false,
  accentSelectedClass = 'bg-indigo-900/40 border-indigo-600',
  preFilter,
  onPick,
  onClose,
}: Props) {
  const [search, setSearch] = useState('')
  const [selectedCategories, setSelectedCategories] = useState<Set<string>>(new Set())
  const [selected, setSelected] = useState<ItemTemplateDto | null>(null)
  const [qty, setQty] = useState(1)

  const source = useMemo(
    () => (preFilter ? catalog.filter(preFilter) : catalog),
    [catalog, preFilter],
  )

  const categories = useMemo(
    () => Array.from(new Set(source.map(t => t.category))).sort(),
    [source],
  )

  const toggleCategory = (cat: string) => {
    setSelectedCategories(prev => {
      const next = new Set(prev)
      if (next.has(cat)) next.delete(cat); else next.add(cat)
      return next
    })
  }

  const filtered = useMemo(() => source.filter(t => {
    const q = search.toLowerCase()
    const matchSearch = q === '' ||
      t.name.toLowerCase().includes(q) ||
      (t.displayName ?? '').toLowerCase().includes(q)
    const matchCat = selectedCategories.size === 0 || selectedCategories.has(t.category)
    return matchSearch && matchCat
  }), [source, search, selectedCategories])

  return (
    <div className="fixed inset-0 bg-black/70 flex items-center justify-center p-4 z-50">
      <div className="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg max-h-[85vh]
                      flex flex-col overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-800 flex items-center justify-between shrink-0">
          <div>
            <h2 className="text-gray-100 font-semibold">{title}</h2>
            {subtitle && <p className="text-gray-500 text-xs mt-0.5">{subtitle}</p>}
          </div>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300 text-lg">×</button>
        </div>

        <div className="flex flex-1 overflow-hidden">
          {categories.length > 1 && (
            <div className="w-36 shrink-0 border-r border-gray-800 px-3 py-3 overflow-y-auto">
              <p className="text-gray-500 text-xs uppercase tracking-wide mb-2">Category</p>
              {categories.map(cat => (
                <label key={cat} className="flex items-center gap-2 py-1 cursor-pointer group">
                  <input type="checkbox" checked={selectedCategories.has(cat)}
                    onChange={() => toggleCategory(cat)}
                    className="rounded border-gray-600 bg-gray-800 text-indigo-500" />
                  <span className={`text-xs ${selectedCategories.has(cat) ? 'text-gray-100' : 'text-gray-400'} group-hover:text-gray-200`}>
                    {cat}
                  </span>
                </label>
              ))}
            </div>
          )}

          <div className="flex-1 flex flex-col overflow-hidden">
            <div className="px-3 pt-3 pb-2 shrink-0">
              <input type="text" placeholder="Search items..." value={search}
                onChange={e => setSearch(e.target.value)} autoFocus
                className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                           text-gray-100 text-sm placeholder-gray-600
                           focus:outline-none focus:border-indigo-500" />
            </div>
            <div className="flex-1 overflow-y-auto px-3 pb-3 space-y-1">
              {filtered.length === 0 ? (
                <p className="text-gray-600 text-sm text-center py-6">No items match.</p>
              ) : (
                filtered.map(t => {
                  const isSelected = selected?.id === t.id
                  return (
                    <button key={t.id} onClick={() => setSelected(t)}
                      className={`w-full text-left px-3 py-2 rounded-lg border transition-colors flex items-center gap-2 ${
                        isSelected ? accentSelectedClass : 'bg-gray-800 border-gray-700 hover:border-gray-600'
                      }`}>
                      <ItemIcon iconUrl={t.iconUrl} alt={t.name} />
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center justify-between gap-2">
                          <span className="text-gray-100 text-sm font-medium truncate">{templateLabel(t)}</span>
                          <div className="flex items-center gap-1.5 shrink-0">
                            {!t.claimable && <span className="text-gray-600 text-xs italic">party only</span>}
                            {t.scenario && (
                              <span className="text-xs px-1.5 rounded"
                                style={{
                                  background: 'var(--badge-purple-bg)',
                                  color: 'var(--badge-purple-color)',
                                }}>
                                {t.scenario}
                              </span>
                            )}
                            <span className={`text-xs ${
                              t.type === 'renewable' ? 'text-purple-400' :
                              t.type === 'equipment' ? 'text-teal-400' : 'text-gray-500'
                            }`}>
                              {t.category}
                            </span>
                          </div>
                        </div>
                        {t.playerDescription && (
                          <p className="text-gray-500 text-xs mt-0.5 truncate">{t.playerDescription}</p>
                        )}
                      </div>
                    </button>
                  )
                })
              )}
            </div>
          </div>
        </div>

        <div className="px-5 py-4 border-t border-gray-800 flex items-center justify-between shrink-0">
          <div className="flex items-center gap-2">
            {!hideQuantity && (
              <>
                <span className="text-gray-400 text-sm">Quantity</span>
                <input type="number" min={1} value={qty}
                  onChange={e => setQty(Math.max(1, Number(e.target.value)))}
                  className="w-16 bg-gray-800 border border-gray-700 rounded px-2 py-1
                             text-gray-100 text-sm text-center focus:outline-none focus:border-indigo-500" />
              </>
            )}
            {selected && <span className="text-gray-500 text-xs">× {templateLabel(selected)}</span>}
          </div>
          <div className="flex gap-2">
            <Button variant="secondary" onClick={onClose}>Cancel</Button>
            <Button onClick={() => selected && onPick(selected.id!, qty)} disabled={!selected}>
              {actionLabel}
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}
