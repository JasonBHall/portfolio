import { useMemo, useRef, useState } from 'react'
import type { ItemTemplateDto } from '../../types'
import { useGameStore } from '../../store/useGameStore'
import { Button } from '../shared/Button'
import { Badge } from '../shared/Badge'
import { ItemIcon } from '../shared/ItemIcon'
import { templateLabel } from '../../utils/item'

const DEFAULT_CATEGORIES = ['Abilities', 'Ammunition', 'Effects', 'Food', 'Fuel', 'General', 'Light', 'Magic']

const BLANK: ItemTemplateDto = {
  name: '', displayName: '', displayNamePlural: '', category: 'General',
  verb: 'use', verbOff: '', type: 'consumable',
  maxQuantity: undefined, minConsumption: undefined, recoveryChance: undefined,
  claimable: true, canGive: true, maxMinutes: undefined, maxFuelMinutes: undefined,
  isReusable: false, acceptedFuelNames: [], minutesPerFuelUnit: undefined,
  recovery: 'none', playerDescription: '', dmDescription: '',
  scenario: undefined, iconUrl: undefined,
}

export function CatalogManager() {
  const {
    catalog, scenarios,
    dmCreateCatalogItem, dmUpdateCatalogItem, dmDeleteCatalogItem,
  } = useGameStore()
  const [editing, setEditing] = useState<ItemTemplateDto | null>(null)
  const [isNew, setIsNew]     = useState(false)
  const [search, setSearch]   = useState('')
  const [selectedCategories, setSelectedCategories] = useState<Set<string>>(new Set())
  const [selectedScenarios,  setSelectedScenarios]  = useState<Set<string>>(new Set())

  const filterCategories = useMemo(
    () => Array.from(new Set(catalog.map(t => t.category))).sort(),
    [catalog],
  )
  const filterScenarios = useMemo(
    () => Array.from(new Set(catalog.map(t => t.scenario).filter((s): s is string => !!s))).sort(),
    [catalog],
  )

  const toggleIn = (set: Set<string>, value: string, setter: (s: Set<string>) => void) => {
    const next = new Set(set)
    if (next.has(value)) next.delete(value); else next.add(value)
    setter(next)
  }

  const filtered = useMemo(() => catalog.filter(t => {
    const q = search.toLowerCase()
    const matchSearch = q === '' ||
      t.name.toLowerCase().includes(q) ||
      (t.displayName ?? '').toLowerCase().includes(q)
    const matchCat = selectedCategories.size === 0 || selectedCategories.has(t.category)
    // Scenario filter: empty set → show all. When selected, special "(none)"
    // sentinel means "untagged items only".
    const scenarioKey = t.scenario ?? '(none)'
    const matchScenario = selectedScenarios.size === 0 || selectedScenarios.has(scenarioKey)
    return matchSearch && matchCat && matchScenario
  }), [catalog, search, selectedCategories, selectedScenarios])

  const grouped = useMemo(() =>
    filtered.reduce<Record<string, ItemTemplateDto[]>>((acc, t) => {
      const cat = t.category || 'General'
      if (!acc[cat]) acc[cat] = []
      acc[cat].push(t)
      return acc
    }, {})
  , [filtered])

  const openNew  = () => { setEditing({ ...BLANK }); setIsNew(true) }
  const openEdit = (t: ItemTemplateDto) => {
    setEditing({ ...t, acceptedFuelNames: [...(t.acceptedFuelNames ?? [])] })
    setIsNew(false)
  }
  const close = () => { setEditing(null); setIsNew(false) }

  const save = async () => {
    if (!editing || !editing.name.trim()) return
    if (isNew) await dmCreateCatalogItem(editing)
    else await dmUpdateCatalogItem(editing)
    close()
  }

  const hasSidebar = filterCategories.length > 1 || filterScenarios.length > 0

  return (
    <div className="flex gap-4">
      {hasSidebar && (
        <div className="w-36 shrink-0">
          <div className="bg-gray-900 border border-gray-800 rounded-xl p-3 sticky top-0 space-y-4">
            {filterCategories.length > 1 && (
              <div>
                <div className="flex items-center justify-between mb-2">
                  <p className="text-gray-500 text-xs uppercase tracking-wide">Category</p>
                  {selectedCategories.size > 0 && (
                    <button onClick={() => setSelectedCategories(new Set())}
                      className="text-indigo-400 hover:text-indigo-300 text-xs">clear</button>
                  )}
                </div>
                <div className="space-y-1.5">
                  {filterCategories.map(cat => {
                    const count = catalog.filter(t => t.category === cat).length
                    return (
                      <label key={cat} className="flex items-center gap-2 cursor-pointer group">
                        <input type="checkbox" checked={selectedCategories.has(cat)}
                          onChange={() => toggleIn(selectedCategories, cat, setSelectedCategories)}
                          className="rounded border-gray-600 bg-gray-800 text-indigo-500" />
                        <span className={`text-xs flex-1 ${
                          selectedCategories.has(cat) ? 'text-gray-100' : 'text-gray-400'
                        } group-hover:text-gray-200`}>{cat}</span>
                        <span className="text-gray-600 text-xs">{count}</span>
                      </label>
                    )
                  })}
                </div>
              </div>
            )}

            {filterScenarios.length > 0 && (
              <div>
                <div className="flex items-center justify-between mb-2">
                  <p className="text-gray-500 text-xs uppercase tracking-wide">Scenario</p>
                  {selectedScenarios.size > 0 && (
                    <button onClick={() => setSelectedScenarios(new Set())}
                      className="text-indigo-400 hover:text-indigo-300 text-xs">clear</button>
                  )}
                </div>
                <div className="space-y-1.5">
                  {/* "(none)" pseudo-option: untagged items only. */}
                  <label className="flex items-center gap-2 cursor-pointer group">
                    <input type="checkbox" checked={selectedScenarios.has('(none)')}
                      onChange={() => toggleIn(selectedScenarios, '(none)', setSelectedScenarios)}
                      className="rounded border-gray-600 bg-gray-800 text-indigo-500" />
                    <span className={`text-xs flex-1 italic ${
                      selectedScenarios.has('(none)') ? 'text-gray-100' : 'text-gray-500'
                    } group-hover:text-gray-200`}>(untagged)</span>
                  </label>
                  {filterScenarios.map(s => {
                    // Lookup display name; fall back to raw id if the scenario
                    // record has been deleted but tagged items still exist.
                    const record = scenarios.find(sc => sc.id === s)
                    const label = record?.name ?? s
                    return (
                      <label key={s} className="flex items-center gap-2 cursor-pointer group">
                        <input type="checkbox" checked={selectedScenarios.has(s)}
                          onChange={() => toggleIn(selectedScenarios, s, setSelectedScenarios)}
                          className="rounded border-gray-600 bg-gray-800 text-indigo-500" />
                        <span className={`text-xs flex-1 ${
                          selectedScenarios.has(s) ? 'text-gray-100' : 'text-gray-400'
                        } group-hover:text-gray-200`}>{label}</span>
                      </label>
                    )
                  })}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      <div className="flex-1 space-y-4 min-w-0">
        <div className="flex gap-2">
          <input type="text" placeholder="Search catalog..." value={search}
            onChange={e => setSearch(e.target.value)}
            className="flex-1 bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                       text-gray-100 text-sm placeholder-gray-600
                       focus:outline-none focus:border-indigo-500" />
          <Button onClick={openNew}>+ New</Button>
        </div>

        {filtered.length === 0 ? (
          <p className="text-gray-600 text-sm text-center py-8">
            {catalog.length === 0 ? 'No catalog items yet.' : 'No items match.'}
          </p>
        ) : (
          Object.entries(grouped).map(([category, items]) => (
            <section key={category}>
              <div className="flex items-center justify-between mb-2">
                <p className="text-xs text-gray-500 uppercase tracking-wide">{category}</p>
                <span className="text-xs text-gray-600">{items.length}</span>
              </div>
              <div className="space-y-1.5">
                {items.map(template => (
                  <CatalogRow key={template.id} template={template} scenarios={scenarios}
                    onEdit={openEdit}
                    onDelete={id => {
                      if (confirm(`Delete "${templateLabel(template)}"?`))
                        dmDeleteCatalogItem(id!)
                    }} />
                ))}
              </div>
            </section>
          ))
        )}
      </div>

      {editing && (
        <CatalogEditor template={editing} isNew={isNew} allTemplates={catalog} scenarios={scenarios}
          onChange={setEditing} onSave={save} onCancel={close} />
      )}
    </div>
  )
}

function CatalogRow({ template, onEdit, onDelete, scenarios }: {
  template: ItemTemplateDto
  scenarios: import('../../types').ScenarioDto[]
  onEdit: (t: ItemTemplateDto) => void
  onDelete: (id: string | undefined) => void
}) {
  const typeColor = template.type === 'renewable' ? 'purple'
    : template.type === 'equipment' ? 'blue' : 'gray'
  const scenarioLabel = template.scenario
    ? (scenarios.find(s => s.id === template.scenario)?.name ?? template.scenario)
    : null

  return (
    <div className="bg-gray-800 border border-gray-700 rounded-lg px-3 py-2.5 flex items-center gap-2">
      <ItemIcon iconUrl={template.iconUrl} alt={template.name} />
      <div className="flex-1 min-w-0 flex items-center gap-2 flex-wrap">
        <span className="text-gray-100 text-sm font-medium truncate">{templateLabel(template)}</span>
        <Badge color={typeColor as 'purple' | 'blue' | 'gray'}>{template.type}</Badge>
        {scenarioLabel && <Badge color="purple">⚡ {scenarioLabel}</Badge>}
        {template.maxMinutes && <Badge color="amber">{template.maxMinutes}m</Badge>}
        {template.isReusable && <Badge color="blue">reusable</Badge>}
        {(template.acceptedFuelNames ?? []).length > 0 && <Badge color="amber">fueled</Badge>}
        {template.recoveryChance != null && template.recoveryChance > 0 && (
          <Badge color="green">{Math.round(template.recoveryChance * 100)}% rec</Badge>
        )}
        {!template.claimable && <Badge color="gray">party only</Badge>}
        {!template.canGive   && <Badge color="red">no give</Badge>}
        {template.dmDescription && <Badge color="purple">DM desc</Badge>}
      </div>
      <div className="flex gap-1.5 shrink-0">
        <Button size="sm" variant="ghost" onClick={() => onEdit(template)}>Edit</Button>
        <Button size="sm" variant="danger" onClick={() => onDelete(template.id)}>×</Button>
      </div>
    </div>
  )
}

function CatalogEditor({
  template, isNew, allTemplates, scenarios, onChange, onSave, onCancel,
}: {
  template: ItemTemplateDto; isNew: boolean
  allTemplates: ItemTemplateDto[]
  scenarios: import('../../types').ScenarioDto[]
  onChange: (t: ItemTemplateDto) => void; onSave: () => void; onCancel: () => void
}) {
  const { dmUploadItemIcon, dmRemoveItemIcon } = useGameStore()

  const set = (patch: Partial<ItemTemplateDto>) => onChange({ ...template, ...patch })
  const isRenewable  = template.type === 'renewable'
  const isConsumable = template.type === 'consumable'
  const isEquipment  = template.type === 'equipment'
  const isTimed      = isEquipment || template.maxMinutes != null

  const [customCategories, setCustomCategories] = useState<string[]>([])
  const [newCatInput, setNewCatInput] = useState('')

  const existingCats = Array.from(new Set(allTemplates.map(t => t.category)))
  const allCategories = Array.from(new Set([...DEFAULT_CATEGORIES, ...existingCats, ...customCategories])).sort()

  const addCategory = () => {
    const v = newCatInput.trim()
    if (!v || allCategories.includes(v)) return
    setCustomCategories(prev => [...prev, v])
    set({ category: v })
    setNewCatInput('')
  }

  const fuelSources = allTemplates.filter(t => t.id !== template.id && t.category === 'Fuel')
  const toggleFuel = (fuelLabel: string) => {
    const current = template.acceptedFuelNames ?? []
    const next = current.includes(fuelLabel)
      ? current.filter(n => n !== fuelLabel)
      : [...current, fuelLabel]
    set({ acceptedFuelNames: next })
  }
  const hasFuels = (template.acceptedFuelNames ?? []).length > 0

  // Icon upload — hidden file input triggered by the visible button
  const fileRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)
  const [uploadError, setUploadError] = useState<string | null>(null)

  const handleIconPick = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    setUploadError(null)
    // Guard: only PNG, <256KB (matches backend cap)
    if (file.type !== 'image/png') {
      setUploadError('PNG only.')
      e.target.value = ''
      return
    }
    if (file.size > 256 * 1024) {
      setUploadError('File too large (max 256 KB).')
      e.target.value = ''
      return
    }
    // Icons are keyed on the server by template id, so a template must
    // exist first. For new templates, prompt the DM to save before icon
    // upload — simpler than queueing the upload and chasing the id.
    if (!template.id) {
      setUploadError('Save the item first, then upload an icon.')
      e.target.value = ''
      return
    }
    setUploading(true)
    try {
      await dmUploadItemIcon(template.id, file)
      // The template re-arrives via ReceiveCatalogUpdate with the new iconUrl,
      // so we don't need to patch `editing` here — but we also don't close
      // the editor, so reflecting the change locally feels immediate.
      // A full page refresh of template happens on next edit open.
    } catch (err) {
      console.error(err); setUploadError('Upload failed.')
    } finally {
      setUploading(false)
      e.target.value = ''
    }
  }

  const handleIconRemove = async () => {
    if (!template.id) return
    await dmRemoveItemIcon(template.id)
    set({ iconUrl: undefined })
  }

  return (
    <div className="fixed inset-0 bg-black/70 flex items-center justify-center p-4 z-50">
      <div className="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md max-h-[90vh] overflow-y-auto">
        <div className="px-5 py-4 border-b border-gray-800 flex items-center justify-between">
          <h2 className="text-gray-100 font-semibold">{isNew ? 'New Catalog Item' : 'Edit Catalog Item'}</h2>
          <button onClick={onCancel} className="text-gray-500 hover:text-gray-300 text-lg">×</button>
        </div>

        <div className="px-5 py-4 space-y-4">
          {/* Icon — top of the form, small enough not to dominate */}
          <Row label="Icon">
            <div className="flex items-center gap-3">
              <div className="w-12 h-12 bg-gray-800 border border-gray-700 rounded flex items-center justify-center shrink-0">
                {template.iconUrl
                  ? <ItemIcon iconUrl={template.iconUrl} size="lg" alt={template.name} />
                  : <span className="text-gray-600 text-xs">none</span>}
              </div>
              <div className="flex-1 flex flex-wrap gap-2">
                <input ref={fileRef} type="file" accept="image/png"
                  onChange={handleIconPick} style={{ display: 'none' }} />
                <Button size="sm" variant="secondary" disabled={uploading}
                  onClick={() => fileRef.current?.click()}>
                  {uploading ? 'Uploading...' : (template.iconUrl ? 'Replace' : 'Upload PNG')}
                </Button>
                {template.iconUrl && (
                  <Button size="sm" variant="ghost" onClick={handleIconRemove}>Remove</Button>
                )}
              </div>
            </div>
            {uploadError && <p className="text-red-400 text-xs mt-1">{uploadError}</p>}
            {isNew && !template.iconUrl && (
              <p className="text-gray-600 text-xs mt-1">
                Save the item first, then reopen it to upload an icon.
              </p>
            )}
          </Row>

          <Row label="Internal Name *">
            <Input value={template.name} onChange={v => set({ name: v })} placeholder="lantern" />
          </Row>

          <div className="grid grid-cols-2 gap-3">
            <Row label="Display Name (singular)">
              <Input value={template.displayName ?? ''} onChange={v => set({ displayName: v || undefined })}
                placeholder="Lantern" />
            </Row>
            <Row label="Display Name (plural)">
              <Input value={template.displayNamePlural ?? ''} onChange={v => set({ displayNamePlural: v || undefined })}
                placeholder="Lanterns" />
            </Row>
          </div>

          <Row label="Category">
            <div className="space-y-1.5">
              <select value={template.category} onChange={e => set({ category: e.target.value })}
                className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                           text-gray-100 text-sm focus:outline-none focus:border-indigo-500">
                {allCategories.map(cat => <option key={cat} value={cat}>{cat}</option>)}
              </select>
              <div className="flex gap-2">
                <input type="text" placeholder="Add custom category..." value={newCatInput}
                  onChange={e => setNewCatInput(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && addCategory()}
                  className="flex-1 bg-gray-800 border border-gray-700 rounded px-2 py-1
                             text-gray-100 text-xs placeholder-gray-600
                             focus:outline-none focus:border-indigo-500" />
                <Button size="sm" variant="secondary" onClick={addCategory}
                  disabled={!newCatInput.trim() || allCategories.includes(newCatInput.trim())}>
                  Add
                </Button>
              </div>
            </div>
          </Row>

          {/* Scenario tag — dropdown sourced from the scenarios table */}
          <Row label="Scenario Tag (optional)">
            <select value={template.scenario ?? ''}
              onChange={e => set({ scenario: e.target.value || undefined })}
              className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                         text-gray-100 text-sm focus:outline-none focus:border-indigo-500">
              <option value="">(untagged — normal world)</option>
              {scenarios.map(s => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
            {scenarios.length === 0 && (
              <p className="text-gray-600 text-xs mt-1">
                No scenarios defined. Open "Manage scenarios…" from the Turn Controls panel to create one.
              </p>
            )}
            {scenarios.length > 0 && (
              <p className="text-gray-600 text-xs mt-1">
                Tagged items only appear to players during a matching active scenario.
              </p>
            )}
          </Row>

          <div className="grid grid-cols-2 gap-3">
            <Row label="Verb (on/use)">
              <Input value={template.verb} onChange={v => set({ verb: v })} placeholder="light" />
            </Row>
            <Row label="Type">
              <div className="flex gap-1.5">
                {(['consumable', 'renewable', 'equipment'] as const).map(t => (
                  <button key={t} onClick={() => set({
                    type: t,
                    recovery: t === 'renewable' ? 'longRest' : 'none',
                    isReusable: t === 'equipment' ? true : template.isReusable,
                  })}
                    className={`flex-1 py-1.5 rounded text-xs font-medium transition-colors ${
                      template.type === t ? 'bg-indigo-700 text-white' : 'bg-gray-800 text-gray-400 hover:text-gray-200'
                    }`}>
                    {t.charAt(0).toUpperCase() + t.slice(1)}
                  </button>
                ))}
              </div>
            </Row>
          </div>

          {isEquipment && (
            <p className="text-gray-600 text-xs -mt-2">Equipment is durable — never auto-removed, burndown bar stays at 0.</p>
          )}

          {isConsumable && (
            <>
              <Row label="Recovery Chance (0–1)">
                <Input type="number" value={template.recoveryChance?.toString() ?? ''}
                  onChange={v => set({ recoveryChance: v ? Number(v) : undefined })} placeholder="0.5" />
              </Row>
              <Row label="Min Consumption">
                <Input type="number" value={template.minConsumption?.toString() ?? ''}
                  onChange={v => set({ minConsumption: v ? Number(v) : undefined })} placeholder="0.5" />
              </Row>
            </>
          )}

          {isRenewable && (
            <>
              <Row label="Max Charges">
                <Input type="number" value={template.maxQuantity?.toString() ?? ''}
                  onChange={v => set({ maxQuantity: v ? Number(v) : undefined })} placeholder="4" />
              </Row>
              <Row label="Recharge On">
                <select value={template.recovery} onChange={e => set({ recovery: e.target.value })}
                  className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                             text-gray-100 text-sm focus:outline-none focus:border-indigo-500">
                  <option value="none">Never</option>
                  <option value="shortRest">Short Rest</option>
                  <option value="longRest">Long Rest</option>
                  <option value="daily">Daily</option>
                </select>
              </Row>
            </>
          )}

          {(isConsumable || isEquipment) && (
            <Row label={isEquipment ? 'Starting Duration (minutes)' : 'Duration (minutes)'}>
              <Input type="number" value={template.maxMinutes?.toString() ?? ''}
                onChange={v => set({ maxMinutes: v ? Number(v) : undefined })}
                placeholder={isEquipment ? '360 for a lantern' : '60 for a torch'} />
            </Row>
          )}

          {isConsumable && isTimed && (
            <label className="flex items-center gap-3 cursor-pointer select-none">
              <Toggle value={template.isReusable}
                onChange={v => set({ isReusable: v, acceptedFuelNames: v ? (template.acceptedFuelNames ?? []) : [] })} />
              <div>
                <span className="text-gray-300 text-sm">Reusable</span>
                <p className="text-gray-600 text-xs">On: snuffing preserves time. Off: stopping discards remaining time.</p>
              </div>
            </label>
          )}

          {(isTimed || template.isReusable) && !isRenewable && (
            <Row label='Off Verb (e.g. "Extinguish", "Power Off", "Snuff")'>
              <Input value={template.verbOff ?? ''} onChange={v => set({ verbOff: v || undefined })} placeholder="Snuff" />
            </Row>
          )}

          {(isEquipment || template.isReusable) && !isRenewable && (
            <div className="space-y-3 border border-gray-700 rounded-lg p-3">
              <p className="text-gray-400 text-xs uppercase tracking-wide">
                Fuel Sources
                <span className="text-gray-600 normal-case ml-1">(items with category "Fuel")</span>
              </p>

              {fuelSources.length === 0 ? (
                <p className="text-gray-600 text-xs">No fuel items yet. Create catalog items with category "Fuel" first.</p>
              ) : (
                <div className="space-y-1.5 max-h-36 overflow-y-auto">
                  {fuelSources.map(t => {
                    const fuelLabel = templateLabel(t)
                    const isChecked = (template.acceptedFuelNames ?? []).includes(fuelLabel)
                    return (
                      <label key={t.id} className="flex items-center gap-2 cursor-pointer">
                        <input type="checkbox" checked={isChecked} onChange={() => toggleFuel(fuelLabel)}
                          className="rounded border-gray-600 bg-gray-800 text-indigo-500" />
                        <span className={`text-xs ${isChecked ? 'text-gray-100' : 'text-gray-400'}`}>{fuelLabel}</span>
                      </label>
                    )
                  })}
                </div>
              )}

              {hasFuels && (
                <>
                  <Row label="Minutes per fuel unit">
                    <Input type="number" value={template.minutesPerFuelUnit?.toString() ?? ''}
                      onChange={v => set({ minutesPerFuelUnit: v ? Number(v) : undefined })} placeholder="60" />
                  </Row>
                  <Row label="Max fuel capacity (minutes)">
                    <Input type="number" value={template.maxFuelMinutes?.toString() ?? ''}
                      onChange={v => set({ maxFuelMinutes: v ? Number(v) : undefined })}
                      placeholder="e.g. 720 — prevents overfilling" />
                  </Row>
                </>
              )}
            </div>
          )}

          <Row label="Player Description">
            <textarea value={template.playerDescription ?? ''}
              onChange={e => set({ playerDescription: e.target.value || undefined })}
              placeholder="What players see" rows={2}
              className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                         text-gray-100 text-sm placeholder-gray-600 resize-none
                         focus:outline-none focus:border-indigo-500" />
          </Row>

          <Row label="DM Description (hidden until revealed)">
            <textarea value={template.dmDescription ?? ''}
              onChange={e => set({ dmDescription: e.target.value || undefined })}
              placeholder="True nature of the item" rows={2}
              className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                         text-gray-100 text-sm placeholder-gray-600 resize-none
                         focus:outline-none focus:border-purple-700" />
          </Row>

          <label className="flex items-center gap-3 cursor-pointer select-none">
            <Toggle value={template.claimable} onChange={v => set({ claimable: v })} />
            <div>
              <span className="text-gray-300 text-sm">Claimable by players</span>
              <p className="text-gray-600 text-xs">Off = party-owned only</p>
            </div>
          </label>

          <label className="flex items-center gap-3 cursor-pointer select-none">
            <Toggle value={template.canGive} onChange={v => set({ canGive: v })} />
            <div>
              <span className="text-gray-300 text-sm">Players can give this away</span>
              <p className="text-gray-600 text-xs">Off = bound to the player (abilities, class features)</p>
            </div>
          </label>
        </div>

        <div className="px-5 py-4 border-t border-gray-800 flex justify-end gap-2">
          <Button variant="secondary" onClick={onCancel}>Cancel</Button>
          <Button onClick={onSave} disabled={!template.name.trim()}>
            {isNew ? 'Create' : 'Save Changes'}
          </Button>
        </div>
      </div>
    </div>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return <div><p className="text-gray-400 text-xs mb-1">{label}</p>{children}</div>
}

function Input({ value, onChange, placeholder, type = 'text' }: {
  value: string; onChange: (v: string) => void; placeholder?: string; type?: string
}) {
  return (
    <input type={type} value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder}
      className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-1.5
                 text-gray-100 text-sm placeholder-gray-600 focus:outline-none focus:border-indigo-500" />
  )
}

function Toggle({ value, onChange }: { value: boolean; onChange: (v: boolean) => void }) {
  return (
    <div onClick={() => onChange(!value)}
      className={`relative w-9 h-5 rounded-full transition-colors cursor-pointer ${
        value ? 'bg-indigo-600' : 'bg-gray-700'
      }`}>
      <div className={`absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white
                      transition-transform ${value ? 'translate-x-4' : 'translate-x-0'}`} />
    </div>
  )
}
