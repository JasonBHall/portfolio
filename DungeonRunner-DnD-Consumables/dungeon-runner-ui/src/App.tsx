import { useEffect, useState, useCallback } from 'react'
import { Routes, Route, useParams, Navigate } from 'react-router-dom'
import { useGameStore } from './store/useGameStore'
import PlayerView from './views/PlayerView'
import DmView from './views/DmView'

type Theme = 'hallowdown' | 'dark' | 'lcars'

// The order users cycle through. Add new themes here and the switcher
// picks them up automatically — nothing else needs to change.
const THEMES: Theme[] = ['hallowdown', 'dark', 'lcars']

const THEME_META: Record<Theme, { label: string; glyph: string }> = {
  hallowdown: { label: 'Hallowdown', glyph: '✦' },
  dark:       { label: 'Dark',       glyph: '◐' },
  lcars:      { label: 'LCARS',      glyph: '⬢' },
}

/**
 * Injects the Google Fonts stylesheet for Antonio (LCARS display font) on
 * first use. Lazy-loaded so users who never touch LCARS don't pay the cost.
 */
function ensureAntonio() {
  if (document.getElementById('dr-font-antonio')) return
  const link = document.createElement('link')
  link.id = 'dr-font-antonio'
  link.rel = 'stylesheet'
  link.href = 'https://fonts.googleapis.com/css2?family=Antonio:wght@400;500;700&display=swap'
  document.head.appendChild(link)
}

/**
 * Personal theme preference. Separate from the scenario-theme override
 * below — when a scenario is active with a theme, the override wins and
 * the personal preference is restored when the scenario ends.
 */
function usePersonalTheme() {
  const [theme, setTheme] = useState<Theme>(() => {
    const saved = localStorage.getItem('dr-theme') as Theme | null
    // Guard against values no longer in our theme set (e.g. an old localStorage
    // entry from a theme that was renamed).
    return saved && THEMES.includes(saved) ? saved : 'hallowdown'
  })

  const cycle = useCallback(() => {
    setTheme(prev => {
      const i = THEMES.indexOf(prev)
      const next = THEMES[(i + 1) % THEMES.length]
      localStorage.setItem('dr-theme', next)
      return next
    })
  }, [])

  return { theme, cycle }
}

/**
 * Forces the document's data-theme to match an override when present,
 * otherwise the user's preference. This is the mechanism behind the DM's
 * "force a theme" ability: a scenario with `theme` set flips every client
 * to that theme, regardless of their personal preference.
 *
 * To add a new theme, drop a `[data-theme="yourname"] { ... }` block into
 * index.css. Then either add it to THEMES above (to expose it to the
 * cycle switcher) or just reference it from a scenario.
 */
function useEffectiveTheme(personal: Theme) {
  const override = useGameStore(s => s.turnState?.activeScenarioTheme)
  useEffect(() => {
    const effective = override || personal
    document.documentElement.setAttribute('data-theme', effective)
    // Antonio is only needed for LCARS; fetch lazily the first time we hit it.
    if (effective === 'lcars') ensureAntonio()
  }, [override, personal])
  return { locked: Boolean(override), override }
}

function ThemeSwitcher({ theme, onCycle, locked }: {
  theme: Theme
  onCycle: () => void
  locked: boolean
}) {
  // When a scenario override is in play we hide the switcher entirely —
  // clicking it would do nothing user-visible, which is more confusing
  // than just not showing it.
  if (locked) return null

  const current = THEME_META[theme]
  // "Next" in the cycle — used as the tooltip so you know what one tap gives you.
  const nextTheme = THEMES[(THEMES.indexOf(theme) + 1) % THEMES.length]
  const next = THEME_META[nextTheme]

  return (
    <button
      onClick={onCycle}
      title={`Theme: ${current.label} — click for ${next.label}`}
      style={{
        position: 'fixed',
        bottom: '16px',
        right: '16px',
        zIndex: 9999,
        background: 'var(--btn-ghost-bg, transparent)',
        border: '1px solid var(--t-border, rgba(55,65,81,1))',
        color: 'var(--t-text-dim, #9ca3af)',
        borderRadius: 'var(--radius, 0.375rem)',
        padding: '6px 10px',
        fontSize: '11px',
        cursor: 'pointer',
        fontFamily: 'var(--font-display, inherit)',
        letterSpacing: '0.08em',
        opacity: 0.7,
        transition: 'opacity 0.15s',
      }}
      onMouseEnter={e => (e.currentTarget.style.opacity = '1')}
      onMouseLeave={e => (e.currentTarget.style.opacity = '0.7')}
    >
      {current.glyph} {current.label}
    </button>
  )
}

export default function App() {
  const { connected, init } = useGameStore()
  const { theme, cycle } = usePersonalTheme()
  const { locked } = useEffectiveTheme(theme)

  useEffect(() => { init() }, [init])

  if (!connected) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="text-gray-400 text-sm mb-2">Connecting to server...</div>
          <div className="w-6 h-6 border-2 border-t-transparent rounded-full animate-spin mx-auto"
            style={{ borderColor: 'var(--t-accent, #6366f1)', borderTopColor: 'transparent' }} />
        </div>
        <ThemeSwitcher theme={theme} onCycle={cycle} locked={locked} />
      </div>
    )
  }

  return (
    <>
      <Routes>
        <Route path="/dm" element={<DmRoute />} />
        <Route path="/:userId" element={<PlayerRoute />} />
        <Route path="/" element={<Navigate to="/dm" replace />} />
      </Routes>
      <ThemeSwitcher theme={theme} onCycle={cycle} locked={locked} />
    </>
  )
}

function DmRoute() {
  const { joined, isDm, joinSession } = useGameStore()

  useEffect(() => {
    if (joined && isDm) return
    joinSession('dm', '', true)
  }, [joined, isDm, joinSession])

  if (!joined) {
    return (
      <div className="min-h-screen flex items-center justify-center text-gray-500 text-sm">
        Joining as DM...
      </div>
    )
  }

  return <DmView />
}

function PlayerRoute() {
  const { userId: urlUserId } = useParams<{ userId: string }>()
  const { joined, isDm, character, joinSession } = useGameStore()

  useEffect(() => {
    if (!urlUserId) return
    if (joined && !isDm && character?.userId === urlUserId.toLowerCase()) return
    joinSession(urlUserId, '', false)
  }, [urlUserId, joined, isDm, character, joinSession])

  if (!joined || !character) {
    return (
      <div className="min-h-screen flex items-center justify-center text-gray-500 text-sm">
        Joining as {urlUserId}...
      </div>
    )
  }

  return <PlayerView />
}
