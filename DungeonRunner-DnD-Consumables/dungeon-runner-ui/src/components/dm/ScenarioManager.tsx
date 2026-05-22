import { useState } from 'react'
import type { ScenarioDto } from '../../types'
import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'
import { Badge } from '../shared/Badge'

/**
 * Modal CRUD editor for scenarios. Ids are immutable — we enforce that
 * by only exposing an "edit" action on name + theme, never id. Deletion
 * is blocked server-side when the scenario is active or has tagged items.
 */
export function ScenarioManager({ onClose }: { onClose: () => void }) {
  const {
    scenarios, turnState,
    dmCreateScenario, dmUpdateScenario, dmDeleteScenario,
  } = useGameStore()

  const [newId, setNewId] = useState('')
  const [newName, setNewName] = useState('')
  const [newTheme, setNewTheme] = useState('')
  const [createError, setCreateError] = useState<string | null>(null)

  const activeId = turnState?.activeScenario

  const handleCreate = async () => {
    setCreateError(null)
    const id = newId.trim()
    const name = newName.trim()
    if (!id) return setCreateError('Id is required.')
    if (/\s/.test(id)) return setCreateError('Id cannot contain spaces.')
    if (scenarios.some(s => s.id.toLowerCase() === id.toLowerCase()))
      return setCreateError('Id already exists.')
    await dmCreateScenario(id, name || id, newTheme.trim() || undefined)
    setNewId(''); setNewName(''); setNewTheme('')
  }

  /** Auto-kebab-case the name into an id on first entry (only if id is empty). */
  const onNameChange = (v: string) => {
    setNewName(v)
    if (!newId.trim()) {
      setNewId(v.trim().toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, ''))
    }
  }

  return (
    <div className="fixed inset-0 bg-black/70 flex items-center justify-center p-4 z-50">
      <div className="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-xl
                      max-h-[85vh] flex flex-col overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-800 flex items-center justify-between">
          <div>
            <h2 className="text-gray-100 font-semibold">Manage Scenarios</h2>
            <p className="text-gray-500 text-xs mt-0.5">
              Scenarios let you swap in themed items mid-campaign. Ids are permanent; names and themes are editable.
            </p>
          </div>
          <button onClick={onClose} className="text-gray-500 hover:text-gray-300 text-lg">×</button>
        </div>

        <div className="flex-1 overflow-y-auto">
          {/* Create form */}
          <div className="px-5 py-4 border-b border-gray-800 bg-gray-950/40">
            <p className="text-gray-400 text-xs uppercase tracking-wide mb-2">New Scenario</p>
            <div className="grid grid-cols-2 gap-2">
              <label className="block">
                <span className="text-gray-500 text-xs mb-1 block">Display Name</span>
                <input type="text" value={newName} onChange={e => onNameChange(e.target.value)}
                  placeholder="Star Trek"
                  className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                             text-gray-100 text-sm focus:outline-none focus:border-indigo-500" />
              </label>
              <label className="block">
                <span className="text-gray-500 text-xs mb-1 block">Internal Id (no spaces, permanent)</span>
                <input type="text" value={newId} onChange={e => setNewId(e.target.value)}
                  placeholder="startrek"
                  className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                             text-gray-100 text-sm font-mono focus:outline-none focus:border-indigo-500" />
              </label>
            </div>
            <label className="block mt-2">
              <span className="text-gray-500 text-xs mb-1 block">Theme key (optional — must match a [data-theme] block in index.css)</span>
              <input type="text" value={newTheme} onChange={e => setNewTheme(e.target.value)}
                placeholder="lcars, christmas, ..."
                className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                           text-gray-100 text-sm focus:outline-none focus:border-indigo-500" />
            </label>
            {createError && <p className="text-red-400 text-xs mt-2">{createError}</p>}
            <div className="mt-3 flex justify-end">
              <Button onClick={handleCreate} disabled={!newId.trim()}>Create</Button>
            </div>
          </div>

          {/* List */}
          <div className="px-5 py-4">
            <p className="text-gray-400 text-xs uppercase tracking-wide mb-2">
              Existing ({scenarios.length})
            </p>
            {scenarios.length === 0 ? (
              <p className="text-gray-600 text-sm text-center py-4">No scenarios yet.</p>
            ) : (
              <div className="space-y-2">
                {scenarios.map(s => (
                  <ScenarioRow key={s.id} scenario={s}
                    isActive={s.id === activeId}
                    onSave={(name, theme) => dmUpdateScenario(s.id, name, theme)}
                    onDelete={() => {
                      if (s.id === activeId) return
                      if (confirm(`Delete scenario "${s.name}"? This is blocked if any items are still tagged with it.`))
                        dmDeleteScenario(s.id)
                    }} />
                ))}
              </div>
            )}
          </div>
        </div>

        <div className="px-5 py-3 border-t border-gray-800 flex justify-end">
          <Button variant="secondary" onClick={onClose}>Close</Button>
        </div>
      </div>
    </div>
  )
}

function ScenarioRow({
  scenario, isActive, onSave, onDelete,
}: {
  scenario: ScenarioDto
  isActive: boolean
  onSave: (name: string, theme?: string) => Promise<void>
  onDelete: () => void
}) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(scenario.name)
  const [theme, setTheme] = useState(scenario.theme ?? '')

  return (
    <div className={`rounded-lg border p-3 ${
      isActive ? 'border-indigo-700/60 bg-indigo-950/30' : 'border-gray-700 bg-gray-800'
    }`}>
      <div className="flex items-center gap-2">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-gray-100 text-sm font-medium">{scenario.name}</span>
            <code className="text-gray-500 text-xs">{scenario.id}</code>
            {scenario.theme && <Badge color="purple">theme: {scenario.theme}</Badge>}
            {isActive && <Badge color="green">active</Badge>}
          </div>
        </div>
        <div className="flex gap-1.5 shrink-0">
          <Button size="sm" variant="ghost" onClick={() => setEditing(e => !e)}>
            {editing ? 'done' : 'edit'}
          </Button>
          <Button size="sm" variant="danger" onClick={onDelete} disabled={isActive}>
            ×
          </Button>
        </div>
      </div>

      {editing && (
        <div className="mt-3 pt-3 border-t border-gray-700 space-y-2">
          <label className="block">
            <span className="text-gray-500 text-xs mb-1 block">Display Name</span>
            <input type="text" value={name} onChange={e => setName(e.target.value)}
              className="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1
                         text-gray-100 text-xs focus:outline-none focus:border-indigo-500" />
          </label>
          <label className="block">
            <span className="text-gray-500 text-xs mb-1 block">Theme (blank = no theme override)</span>
            <input type="text" value={theme} onChange={e => setTheme(e.target.value)}
              placeholder="lcars, christmas..."
              className="w-full bg-gray-700 border border-gray-600 rounded px-2 py-1
                         text-gray-100 text-xs focus:outline-none focus:border-indigo-500" />
          </label>
          <div className="flex justify-end">
            <Button size="sm" onClick={() => { onSave(name, theme.trim() || undefined); setEditing(false) }}>
              Save
            </Button>
          </div>
          {isActive && (
            <p className="text-indigo-400 text-xs">
              This scenario is active — theme change will apply live to all clients.
            </p>
          )}
        </div>
      )}
    </div>
  )
}
