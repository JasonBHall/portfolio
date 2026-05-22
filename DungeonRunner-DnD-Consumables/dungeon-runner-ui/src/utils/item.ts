// Small display helpers used across the UI. These used to be inlined in
// every component — centralizing them cuts a lot of repetition and keeps
// item naming/display conventions consistent.
import type { ItemDto, ItemTemplateDto, LootItemDto } from '../types'

/** Label for an item, respecting displayName and plurals. */
export function itemLabel(
  item: { name: string; displayName?: string; displayNamePlural?: string },
  quantity?: number,
): string {
  const singular = item.displayName ?? item.name
  if (quantity === undefined) return singular
  const plural = item.displayNamePlural ?? singular
  return quantity === 1 ? singular : plural
}

/** Template label — simpler because templates don't carry a quantity. */
export function templateLabel(t: ItemTemplateDto): string {
  return t.displayName ?? t.name
}

/** Loot item label — currently just .name but gives us one place to tweak later. */
export function lootLabel(l: LootItemDto): string {
  return l.name
}

/** Capitalize a verb for display (e.g. "light" → "Light"). Handles empty/undefined. */
export function capVerb(verb: string | undefined): string {
  if (!verb) return ''
  return verb.charAt(0).toUpperCase() + verb.slice(1)
}

/** Group a list of items by their `category` string. Preserves insertion order within a group. */
export function groupByCategory<T extends { category: string }>(items: T[]): Record<string, T[]> {
  return items.reduce<Record<string, T[]>>((acc, item) => {
    const cat = item.category || 'General'
    if (!acc[cat]) acc[cat] = []
    acc[cat].push(item)
    return acc
  }, {})
}

/** Short human label for a time-mode enum. */
export function timeModeLabel(mode: string): string {
  switch (mode) {
    case 'dungeon':        return '10 min/turn'
    case 'unknownOpenAir': return '1 hr/turn'
    case 'openAir':        return '1 day/turn'
    default:               return mode
  }
}

/** True when an item is at or below 25% on either charges or duration — used for "critical" styling. */
export function isItemCritical(item: ItemDto): boolean {
  if (item.remainingMinutes != null && item.maxMinutes != null && item.maxMinutes > 0) {
    if (item.remainingMinutes / item.maxMinutes <= 0.25) return true
  }
  if (item.type === 'renewable' && item.maxQuantity != null && item.maxQuantity > 0) {
    const charges = item.charges ?? item.maxQuantity
    if (charges / item.maxQuantity <= 0.25) return true
  }
  return false
}
