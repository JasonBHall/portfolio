import { create } from 'zustand'
import { connection, startConnection } from '../connection/hubService'
import {
  CharacterDto, PartyDto, TurnStateDto, ActionLogEntryDto,
  ItemTemplateDto, EncounterDto, ScenarioDto,
} from '../types'

export interface DMAdjustFields {
  quantity?: number
  charges?: number
  remainingMinutes?: number
  maxQuantity?: number
  playerDescription?: string
  dmDescription?: string
  dmDescriptionRevealed?: boolean
  isPinned?: boolean
  /** When `updateScenario: true`, this value is applied (null clears). */
  scenario?: string | null
  /** Gate flag — see DMAdjustItemRequest on the backend for the rationale. */
  updateScenario?: boolean
}

interface GameState {
  connected: boolean; joined: boolean; userId: string; isDm: boolean
  character: CharacterDto | null; party: PartyDto | null; turnState: TurnStateDto | null
  allCharacters: CharacterDto[]; partyRoster: CharacterDto[]
  catalog: ItemTemplateDto[]; encounters: EncounterDto[]
  scenarios: ScenarioDto[]
  actionLog: ActionLogEntryDto[]; notifications: string[]; playerNotifications: string[]

  init: () => Promise<void>
  joinSession: (userId: string, characterName: string, isDm: boolean) => Promise<void>

  // Player actions
  useItem: (itemId: string, quantity?: number) => Promise<void>
  recoverItem: (itemId: string) => Promise<void>
  lightItem: (itemId: string) => Promise<void>
  snuffItem: (itemId: string) => Promise<void>
  spendRenewable: (itemId: string, amount?: number) => Promise<void>
  shortRest: () => Promise<void>
  longRest: () => Promise<void>
  pinItem: (itemId: string, pinned: boolean) => Promise<void>
  giveItemToParty: (itemId: string, quantity: number) => Promise<void>
  giveItemToPlayer: (toUserId: string, itemId: string, quantity: number) => Promise<void>
  fuelItem: (itemId: string, fuelItemId: string, quantity: number) => Promise<void>
  claimLootToPlayer: (lootItemId: string, quantity: number) => Promise<void>
  claimLootToParty: (lootItemId: string, quantity: number) => Promise<void>
  claimFromPartyToPlayer: (itemId: string, quantity: number) => Promise<void>
  usePartyItem: (itemId: string, quantity?: number) => Promise<void>

  // DM — turn/time
  dmAdvanceTurn: () => Promise<void>
  dmResetTurn: () => Promise<void>
  dmChangeTimeMode: (timeMode: string) => Promise<void>

  // DM — scenarios
  dmStartScenario: (scenarioId: string) => Promise<void>
  dmEndScenario: () => Promise<void>
  dmCreateScenario: (id: string, name: string, theme?: string) => Promise<void>
  dmUpdateScenario: (id: string, name: string, theme?: string) => Promise<void>
  dmDeleteScenario: (id: string) => Promise<void>

  // DM — loot / party inventory
  dmAddLoot: (name: string, quantity: number) => Promise<void>
  dmAddLootFromCatalog: (templateId: string, quantity: number) => Promise<void>
  dmClearLootBox: () => Promise<void>
  dmEditLootItem: (lootItemId: string, quantity: number | undefined, playerDescription: string, dmDescription: string) => Promise<void>
  dmDeleteLootItem: (lootItemId: string) => Promise<void>
  dmAddPartyItem: (name: string, quantity: number) => Promise<void>
  dmAddPartyItemFromCatalog: (templateId: string, quantity: number) => Promise<void>
  dmEditPartyItem: (itemId: string, quantity: number | undefined, playerDescription: string, dmDescription: string) => Promise<void>
  dmDeletePartyItem: (itemId: string) => Promise<void>

  // DM — encounters
  dmCreateEncounter: (name: string, scenario?: string) => Promise<void>
  dmAddItemToEncounter: (encounterId: string, name: string, quantity: number) => Promise<void>
  dmAddCatalogItemToEncounter: (encounterId: string, templateId: string, quantity: number) => Promise<void>
  dmEditEncounterItem: (encounterId: string, lootItemId: string, quantity: number | undefined, playerDescription: string, dmDescription: string) => Promise<void>
  dmRemoveItemFromEncounter: (encounterId: string, lootItemId: string) => Promise<void>
  dmPushEncounterToLootBox: (encounterId: string) => Promise<void>
  dmPushSingleEncounterItem: (encounterId: string, lootItemId: string) => Promise<void>
  dmDeleteEncounter: (encounterId: string) => Promise<void>

  // DM — player items
  dmDeleteItem: (userId: string, itemId: string) => Promise<void>
  dmAdjustItem: (userId: string, itemId: string, fields: DMAdjustFields) => Promise<void>
  dmGiveItem: (userId: string, templateId: string, quantity: number) => Promise<void>

  // DM — catalog + icons
  dmCreateCatalogItem: (template: ItemTemplateDto) => Promise<void>
  dmUpdateCatalogItem: (template: ItemTemplateDto) => Promise<void>
  dmDeleteCatalogItem: (templateId: string) => Promise<void>
  dmUploadItemIcon: (templateId: string, pngFile: File) => Promise<void>
  dmRemoveItemIcon: (templateId: string) => Promise<void>

  // DM — rest / player admin
  dmRest: (userId: string, type: string) => Promise<void>
  dmPartyShortRest: () => Promise<void>
  dmPartyLongRest: () => Promise<void>
  dmCreatePlayer: (userId: string, characterName: string) => Promise<void>

  dismissNotification: (index: number) => void
  dismissPlayerNotification: (index: number) => void
}

let initialized = false

/** Reads a File as base64 (strips the data-URL prefix). Used for icon upload. */
function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload  = () => {
      const result = reader.result as string
      // "data:image/png;base64,iVBOR..." — we only want the base64 tail
      const i = result.indexOf(',')
      resolve(i >= 0 ? result.slice(i + 1) : result)
    }
    reader.onerror = () => reject(reader.error ?? new Error('read failed'))
    reader.readAsDataURL(file)
  })
}

export const useGameStore = create<GameState>((set, get) => ({
  connected: false, joined: false, userId: '', isDm: false,
  character: null, party: null, turnState: null,
  allCharacters: [], partyRoster: [], catalog: [], encounters: [], scenarios: [],
  actionLog: [], notifications: [], playerNotifications: [],

  init: async () => {
    if (initialized) return
    initialized = true

    connection.on('ReceivePersonalUpdate', (char: CharacterDto) => {
      set(state => {
        if (state.isDm) {
          const chars = [...state.allCharacters]
          const idx = chars.findIndex(c => c.userId === char.userId)
          if (idx >= 0) chars[idx] = char; else chars.push(char)
          return { allCharacters: chars }
        }
        if (char.userId === state.userId) return { character: char }
        return {}
      })
    })
    connection.on('ReceiveRosterUpdate', (char: CharacterDto) => {
      set(state => {
        const roster = [...state.partyRoster]
        const idx = roster.findIndex(c => c.userId === char.userId)
        if (idx >= 0) roster[idx] = char; else roster.push(char)
        return { partyRoster: roster }
      })
    })
    connection.on('ReceivePartyUpdate',    (party: PartyDto)       => set({ party }))
    connection.on('ReceiveTurnUpdate',     (turnState: TurnStateDto) => set({ turnState }))
    connection.on('ReceiveCatalogUpdate',  (catalog: ItemTemplateDto[]) => set({ catalog }))
    connection.on('ReceiveEncounterUpdate',(encounters: EncounterDto[]) => set({ encounters }))
    connection.on('ReceiveScenarioListUpdate',(scenarios: ScenarioDto[]) => set({ scenarios }))
    connection.on('ReceiveActionLog', (entry: ActionLogEntryDto) => {
      set(state => ({ actionLog: [...state.actionLog, entry].slice(-200) }))
    })
    connection.on('ReceiveNotification', (message: string) => {
      set(state => ({ notifications: [...state.notifications, message] }))
      setTimeout(() => set(state => ({ notifications: state.notifications.slice(1) })), 6000)
    })
    connection.on('ReceivePlayerNotification', (message: string) => {
      set(state => ({ playerNotifications: [...state.playerNotifications, message] }))
      setTimeout(() => set(state => ({ playerNotifications: state.playerNotifications.slice(1) })), 8000)
    })
    connection.onreconnected(async () => {
      set({ connected: true })
      const { userId, isDm } = get()
      if (userId) await connection.invoke('JoinSession', { userId, characterName: '', isDm })
    })
    connection.onreconnecting(() => set({ connected: false }))
    connection.onclose(()        => set({ connected: false }))

    try { await startConnection(); set({ connected: true }) }
    catch (err) { console.error('[Hub] Failed to connect:', err) }
  },

  joinSession: async (userId, characterName, isDm) => {
    const normalized = isDm ? 'dm' : userId.trim().toLowerCase()
    await connection.invoke('JoinSession', { userId: normalized, characterName, isDm })
    set({ userId: normalized, isDm, joined: true })
  },

  // ---------------------------------------------------------
  // Player actions — all pass userId from the store
  // ---------------------------------------------------------
  useItem:           async (itemId, quantity = 1) => connection.invoke('UseItem',           { userId: get().userId, itemId, quantity }),
  recoverItem:       async (itemId)               => connection.invoke('RecoverItem',       { userId: get().userId, itemId }),
  lightItem:         async (itemId)               => connection.invoke('LightItem',         { userId: get().userId, itemId }),
  snuffItem:         async (itemId)               => connection.invoke('SnuffItem',         { userId: get().userId, itemId }),
  spendRenewable:    async (itemId, amount = 1)   => connection.invoke('SpendRenewable',    { userId: get().userId, itemId, amount }),
  shortRest:         async ()                     => connection.invoke('ShortRest',         { userId: get().userId, type: 'short' }),
  longRest:          async ()                     => connection.invoke('LongRest',          { userId: get().userId, type: 'long' }),
  pinItem:           async (itemId, pinned)       => connection.invoke('PinItem',           { userId: get().userId, itemId, pinned }),
  giveItemToParty:   async (itemId, quantity)     => connection.invoke('GiveItemToParty',   { userId: get().userId, itemId, quantity }),
  giveItemToPlayer:  async (toUserId, itemId, quantity) =>
    connection.invoke('GiveItemToPlayer', { fromUserId: get().userId, toUserId, itemId, quantity }),
  fuelItem:          async (itemId, fuelItemId, quantity) =>
    connection.invoke('FuelItem', { userId: get().userId, itemId, fuelItemId, quantity }),
  claimLootToPlayer:     async (lootItemId, quantity) => connection.invoke('ClaimLootToPlayer',     { userId: get().userId, lootItemId, quantity }),
  claimLootToParty:      async (lootItemId, quantity) => connection.invoke('ClaimLootToParty',      { lootItemId, quantity }),
  claimFromPartyToPlayer:async (itemId, quantity)     => connection.invoke('ClaimFromPartyToPlayer',{ userId: get().userId, itemId, quantity }),
  usePartyItem:          async (itemId, quantity = 1) => connection.invoke('UsePartyItem',          { userId: get().userId, itemId, quantity }),

  // ---------------------------------------------------------
  // DM — turn / time / scenario
  // ---------------------------------------------------------
  dmAdvanceTurn:    async ()          => connection.invoke('DMAdvanceTurn'),
  dmResetTurn:      async ()          => connection.invoke('DMResetTurn'),
  dmChangeTimeMode: async (timeMode)  => connection.invoke('DMChangeTimeMode', { timeMode }),

  dmStartScenario:  async (scenarioId)    => connection.invoke('DMStartScenario', { scenarioId }),
  dmEndScenario:    async ()              => connection.invoke('DMEndScenario'),
  dmCreateScenario: async (id, name, theme) => connection.invoke('DMCreateScenario', { id, name, theme: theme || null }),
  dmUpdateScenario: async (id, name, theme) => connection.invoke('DMUpdateScenario', { id, name, theme: theme || null }),
  dmDeleteScenario: async (id)            => connection.invoke('DMDeleteScenario', { id }),

  // ---------------------------------------------------------
  // DM — loot + party inventory
  // ---------------------------------------------------------
  dmAddLoot:             async (name, quantity)        => connection.invoke('DMAddLoot',             { name, quantity }),
  dmAddLootFromCatalog:  async (templateId, quantity)  => connection.invoke('DMAddLootFromCatalog',  { templateId, quantity }),
  dmClearLootBox:        async ()                      => connection.invoke('DMClearLootBox'),
  dmEditLootItem:        async (lootItemId, quantity, playerDescription, dmDescription) =>
    connection.invoke('DMEditLootItem', { lootItemId, quantity, playerDescription, dmDescription }),
  dmDeleteLootItem:      async (lootItemId)            => connection.invoke('DMDeleteLootItem',      { lootItemId }),

  dmAddPartyItem:            async (name, quantity)       => connection.invoke('DMAddPartyItem',            { name, quantity }),
  dmAddPartyItemFromCatalog: async (templateId, quantity) => connection.invoke('DMAddPartyItemFromCatalog', { templateId, quantity }),
  dmEditPartyItem:           async (itemId, quantity, playerDescription, dmDescription) =>
    connection.invoke('DMEditPartyItem', { itemId, quantity, playerDescription, dmDescription }),
  dmDeletePartyItem:         async (itemId)               => connection.invoke('DMDeletePartyItem',         { itemId }),

  // ---------------------------------------------------------
  // DM — encounters
  // ---------------------------------------------------------
  dmCreateEncounter:   async (name, scenario) => connection.invoke('DMCreateEncounter', { name, scenario: scenario || null }),
  dmAddItemToEncounter:        async (encounterId, name, quantity)        => connection.invoke('DMAddItemToEncounter',        { encounterId, name, quantity }),
  dmAddCatalogItemToEncounter: async (encounterId, templateId, quantity)  => connection.invoke('DMAddCatalogItemToEncounter', { encounterId, templateId, quantity }),
  dmEditEncounterItem: async (encounterId, lootItemId, quantity, playerDescription, dmDescription) =>
    connection.invoke('DMEditEncounterItem', { encounterId, lootItemId, quantity, playerDescription, dmDescription }),
  dmRemoveItemFromEncounter: async (encounterId, lootItemId) => connection.invoke('DMRemoveItemFromEncounter', { encounterId, lootItemId }),
  dmPushEncounterToLootBox:  async (encounterId)             => connection.invoke('DMPushEncounterToLootBox',  { encounterId }),
  dmPushSingleEncounterItem: async (encounterId, lootItemId) => connection.invoke('DMPushSingleEncounterItem', { encounterId, lootItemId }),
  dmDeleteEncounter:         async (encounterId)             => connection.invoke('DMDeleteEncounter',         { encounterId }),

  // ---------------------------------------------------------
  // DM — player-owned items
  // ---------------------------------------------------------
  dmDeleteItem: async (userId, itemId) => connection.invoke('DMDeleteItem', { userId, itemId }),
  dmAdjustItem: async (userId, itemId, fields) =>
    connection.invoke('DMAdjustItem', { userId, itemId, ...fields }),
  dmGiveItem:   async (userId, templateId, quantity) => connection.invoke('DMGiveItem', { userId, templateId, quantity }),

  // ---------------------------------------------------------
  // DM — catalog
  //
  // The backend now accepts ItemTemplateDto directly, so we just spread
  // the template. One line replaces the old 14-line payload mapping.
  // ---------------------------------------------------------
  dmCreateCatalogItem: async (t) => connection.invoke('DMCreateCatalogItem', t),
  dmUpdateCatalogItem: async (t) => connection.invoke('DMUpdateCatalogItem', t),
  dmDeleteCatalogItem: async (templateId) => connection.invoke('DMDeleteCatalogItem', { templateId }),

  dmUploadItemIcon: async (templateId, file) => {
    const base64Png = await fileToBase64(file)
    await connection.invoke('DMUploadItemIcon', { templateId, base64Png })
  },
  dmRemoveItemIcon: async (templateId) => connection.invoke('DMRemoveItemIcon', { templateId }),

  // ---------------------------------------------------------
  // DM — rest + players
  // ---------------------------------------------------------
  dmRest:            async (userId, type)     => connection.invoke('DMRest',            { userId, type }),
  dmPartyShortRest:  async ()                 => connection.invoke('DMPartyShortRest'),
  dmPartyLongRest:   async ()                 => connection.invoke('DMPartyLongRest'),
  dmCreatePlayer:    async (userId, characterName) => connection.invoke('DMCreatePlayer', { userId, characterName }),

  dismissNotification:       (index) => set(state => ({ notifications:       state.notifications.filter((_, i) => i !== index) })),
  dismissPlayerNotification: (index) => set(state => ({ playerNotifications: state.playerNotifications.filter((_, i) => i !== index) })),
}))
