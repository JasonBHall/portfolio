import { ButtonHTMLAttributes, CSSProperties } from 'react'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  size?: 'sm' | 'md'
}

export function Button({
  variant = 'primary',
  size = 'md',
  className = '',
  style,
  children,
  ...props
}: ButtonProps) {
  const variantStyles: Record<string, CSSProperties> = {
    primary: {
      background: 'var(--btn-primary-bg)',
      border: '1px solid var(--btn-primary-border)',
      color: 'var(--btn-primary-color)',
    },
    secondary: {
      background: 'var(--btn-secondary-bg)',
      border: '1px solid var(--btn-secondary-border)',
      color: 'var(--btn-secondary-color)',
    },
    danger: {
      background: 'var(--btn-danger-bg)',
      border: '1px solid var(--btn-danger-border)',
      color: 'var(--btn-danger-color)',
    },
    ghost: {
      background: 'var(--btn-ghost-bg)',
      border: '1px solid transparent',
      color: 'var(--btn-ghost-color)',
    },
  }

  const sizeClass = size === 'sm' ? 'px-2.5 py-1 text-xs' : 'px-3.5 py-1.5 text-sm'

  return (
    <button
      className={`font-medium transition-all duration-150
        disabled:opacity-40 disabled:cursor-not-allowed
        ${sizeClass} ${className}`}
      style={{
        borderRadius: 'var(--radius, 0.375rem)',
        ...variantStyles[variant],
        ...style,
      }}
      {...props}
    >
      {children}
    </button>
  )
}
