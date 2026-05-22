import { CSSProperties, ReactNode } from 'react'

interface BadgeProps {
  children: ReactNode
  color?: 'blue' | 'green' | 'amber' | 'red' | 'purple' | 'gray'
}

export function Badge({ children, color = 'gray' }: BadgeProps) {
  const style: CSSProperties = {
    background: `var(--badge-${color}-bg)`,
    color: `var(--badge-${color}-color)`,
    border: `1px solid var(--badge-${color}-color, transparent)`,
    opacity: 0.9,
    borderRadius: 'var(--radius, 0.25rem)',
  }

  return (
    <span
      className="inline-block text-xs px-1.5 py-0.5 font-medium"
      style={style}
    >
      {children}
    </span>
  )
}
