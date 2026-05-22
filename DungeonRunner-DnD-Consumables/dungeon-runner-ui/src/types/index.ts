export type ItemType = 'consumable' | 'renewable' | 'equipment'
export type RecoveryType = 'none' | 'shortRest' | 'longRest' | 'daily'
export type TimeMode = 'dungeon' | 'unknownOpenAir' | 'openAir'

export interface ItemDto {
  id: string
  name: string
  displayName?: string
  displayNamePlural?: string
  category: string
  verb: string
  verbOff?: string
  type: ItemType
  quantity: number
  charges?: number
  maxQuantity?: number
  minConsumption?: number
  recoveryChance?: number
  claimable: boolean
  canGive: boolean
  isActive: boolean
  maxMinutes?: number
  remainingMinutes?: number
  maxFuelMinutes?: number
  isReusable: boolean
  acceptedFuelNames: string[]
  minutesPerFuelUnit?: number
  recovery: RecoveryType
  playerDescription?: string
  dmDescription?: string
  dmDescriptionRevealed: boolean
  isPinned: boolean
  scenario?: string
  iconUrl?: string
}

export interface ItemTemplateDto {
  id?: string
  name: string
  displayName?: string
  displayNamePlural?: string
  category: string
  verb: string
  verbOff?: string
  type: string
  maxQuantity?: number
  minConsumption?: number
  recoveryChance?: number
  claimable: boolean
  canGive: boolean
  maxMinutes?: number
  maxFuelMinutes?: number
  isReusable: boolean
  acceptedFuelNames: string[]
  minutesPerFuelUnit?: number
  recovery: string
  playerDescription?: string
  dmDescription?: string
  scenario?: string
  iconUrl?: string
}

export interface PlayerEffectDto {
  name: string
  description?: string
  remainingTurns: number
}

export interface CharacterDto {
  userId: string
  name: string
  items: ItemDto[]
  effects: PlayerEffectDto[]
}

export interface LootItemDto {
  id: string
  name: string
  quantity: number
  playerDescription?: string
  dmDescription?: string
  claimable: boolean
  scenario?: string
  iconUrl?: string
}

export interface EncounterDto {
  id: string
  name: string
  items: LootItemDto[]
  scenario?: string
}

export interface PartyDto {
  lootBox: LootItemDto[]
  inventory: ItemDto[]
}

export interface TurnStateDto {
  currentTurn: number
  timeMode: TimeMode
  activeScenario?: string
  activeScenarioTheme?: string
}

export interface ScenarioDto {
  id: string
  name: string
  theme?: string
}

export interface ActionLogEntryDto {
  userId: string
  characterName: string
  action: string
  timestamp: string
}
