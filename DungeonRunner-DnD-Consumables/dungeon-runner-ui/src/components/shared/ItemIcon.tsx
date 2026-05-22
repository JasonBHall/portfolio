import type { CSSProperties } from 'react'

// Size presets. Keep the set small so usage is consistent across views.
//   sm — read-only rows (MemberCard, tight DM rows)
//   md — default (ItemCard, picker modal rows, inventory cards)
//   lg — catalog editor preview
const SIZES: Record<string, number> = { sm: 16, md: 24, lg: 48 }

interface Props {
  iconUrl?: string
  size?: 'sm' | 'md' | 'lg'
  /** Accessible alt text; usually the item name. */
  alt?: string
  /** Overrides the preset dimension when you need a specific pixel size. */
  pixels?: number
  style?: CSSProperties
}

/**
 * Renders a square icon for an item. When `iconUrl` is falsy, renders
 * nothing — by design, the surrounding layout collapses (confirmed with
 * you: icon absent → layout shifts, no reserved slot).
 */
export function ItemIcon({ iconUrl, size = 'md', alt = '', pixels, style }: Props) {
  if (!iconUrl) return null
  const px = pixels ?? SIZES[size]
  return (
    <img
      src={iconUrl}
      alt={alt}
      width={px}
      height={px}
      className="shrink-0"
      style={{
        width: px,
        height: px,
        objectFit: 'contain',
        imageRendering: 'pixelated',   // crisp scaling for small PNGs
        ...style,
      }}
    />
  )
}
