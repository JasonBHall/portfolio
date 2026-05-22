namespace DungeonRunner.Hubs;

// Records give us init-only positional properties with defaults, serialize
// fine through SignalR's camelCase JSON protocol, and collapse what was
// ~45 lines of boilerplate into roughly a third the space.

public record JoinSessionRequest(string UserId = "", string CharacterName = "", bool IsDm = false);
public record ItemActionRequest(string UserId = "", string ItemId = "", int Quantity = 1);
public record RecoverItemRequest(string UserId = "", string ItemId = "");
public record LightItemRequest(string UserId = "", string ItemId = "");
public record SpendRenewableRequest(string UserId = "", string ItemId = "", int Amount = 1);
public record RestRequest(string UserId = "", string Type = "short");
public record PinItemRequest(string UserId = "", string ItemId = "", bool Pinned = false);
public record GiveItemToPartyRequest(string UserId = "", string ItemId = "", int Quantity = 1);
public record GiveItemToPlayerRequest(string FromUserId = "", string ToUserId = "", string ItemId = "", int Quantity = 1);
public record FuelItemRequest(string UserId = "", string ItemId = "", string FuelItemId = "", int Quantity = 1);
public record ClaimLootToPlayerRequest(string UserId = "", string LootItemId = "", int Quantity = 1);
public record ClaimLootToPartyRequest(string LootItemId = "", int Quantity = 1);
public record ClaimFromPartyToPlayerRequest(string UserId = "", string ItemId = "", int Quantity = 1);
public record UsePartyItemRequest(string UserId = "", string ItemId = "", int Quantity = 1);
public record DMCreatePlayerRequest(string UserId = "", string CharacterName = "");

// Loot box
public record DMAddLootRequest(string Name = "", int Quantity = 1);
public record DMAddLootFromCatalogRequest(string TemplateId = "", int Quantity = 1);
public record DMEditLootItemRequest(string LootItemId = "", int? Quantity = null, string? PlayerDescription = null, string? DmDescription = null);
public record DMDeleteLootItemRequest(string LootItemId = "");

// Party inventory
public record DMAddPartyItemRequest(string Name = "", int Quantity = 1);
public record DMAddPartyItemFromCatalogRequest(string TemplateId = "", int Quantity = 1);
public record DMEditPartyItemRequest(string ItemId = "", int? Quantity = null, string? PlayerDescription = null, string? DmDescription = null);
public record DMDeletePartyItemRequest(string ItemId = "");

// Player item controls. Scenario is mutable here so the DM can retag an
// existing item (null clears, any string sets).
public record DMDeleteItemRequest(string UserId = "", string ItemId = "");
public record DMAdjustItemRequest(
    string UserId = "",
    string ItemId = "",
    int? Quantity = null,
    int? Charges = null,
    int? RemainingMinutes = null,
    int? MaxQuantity = null,
    string? PlayerDescription = null,
    string? DmDescription = null,
    bool? DmDescriptionRevealed = null,
    bool? IsPinned = null,
    string? Scenario = null,
    bool UpdateScenario = false);   // true → apply Scenario even if null (to clear)
public record DMGiveItemRequest(string UserId = "", string TemplateId = "", int Quantity = 1);

// Encounters
public record DMCreateEncounterRequest(string Name = "", string? Scenario = null);
public record DMAddItemToEncounterRequest(string EncounterId = "", string Name = "", int Quantity = 1);
public record DMAddCatalogItemToEncounterRequest(string EncounterId = "", string TemplateId = "", int Quantity = 1);
public record DMEditEncounterItemRequest(string EncounterId = "", string LootItemId = "", int? Quantity = null, string? PlayerDescription = null, string? DmDescription = null);
public record DMRemoveItemFromEncounterRequest(string EncounterId = "", string LootItemId = "");
public record DMPushEncounterRequest(string EncounterId = "");
public record DMPushSingleEncounterItemRequest(string EncounterId = "", string LootItemId = "");
public record DMDeleteEncounterRequest(string EncounterId = "");

// Catalog — hub now consumes ItemTemplateDto directly, so no wrapper request.
public record DeleteCatalogItemRequest(string TemplateId = "");

// Turn / time mode
public record ChangeTimeModeRequest(string TimeMode = "Dungeon");

// Icons
public record DMUploadItemIconRequest(string TemplateId = "", string Base64Png = "");
public record DMRemoveItemIconRequest(string TemplateId = "");

// Scenario control
public record DMStartScenarioRequest(string ScenarioId = "");
public record DMCreateScenarioRequest(string Id = "", string Name = "", string? Theme = null);
public record DMUpdateScenarioRequest(string Id = "", string Name = "", string? Theme = null);
public record DMDeleteScenarioRequest(string Id = "");
// DMEndScenario takes no payload.
