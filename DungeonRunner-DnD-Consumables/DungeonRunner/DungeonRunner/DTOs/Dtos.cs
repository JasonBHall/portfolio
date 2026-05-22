using DungeonRunner.Domain;

namespace DungeonRunner.DTOs;

public class ItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? DisplayNamePlural { get; set; }
    public string Category { get; set; } = "General";
    public string Verb { get; set; } = string.Empty;
    public string? VerbOff { get; set; }
    public ItemType Type { get; set; } = ItemType.Consumable;
    public int Quantity { get; set; }
    public int? Charges { get; set; }
    public int? MaxQuantity { get; set; }
    public double? MinConsumption { get; set; }
    public double? RecoveryChance { get; set; }
    public bool Claimable { get; set; } = true;
    public bool CanGive { get; set; } = true;
    public bool IsActive { get; set; }
    public int? MaxMinutes { get; set; }
    public int? RemainingMinutes { get; set; }
    public int? MaxFuelMinutes { get; set; }
    public bool IsReusable { get; set; }
    public List<string> AcceptedFuelNames { get; set; } = new();
    public int? MinutesPerFuelUnit { get; set; }
    public RecoveryType Recovery { get; set; } = RecoveryType.None;
    public string? PlayerDescription { get; set; }
    public string? DmDescription { get; set; }
    public bool DmDescriptionRevealed { get; set; }
    public bool IsPinned { get; set; }
    public string? Scenario { get; set; }
    public string? IconUrl { get; set; }   // resolved from template at map-time
}

public class ItemTemplateDto
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? DisplayNamePlural { get; set; }
    public string Category { get; set; } = "General";
    public string Verb { get; set; } = string.Empty;
    public string? VerbOff { get; set; }
    // Enum-as-string on the wire (frontend sends "consumable", "renewable", "equipment").
    public string Type { get; set; } = "consumable";
    public int? MaxQuantity { get; set; }
    public double? MinConsumption { get; set; }
    public double? RecoveryChance { get; set; }
    public bool Claimable { get; set; } = true;
    public bool CanGive { get; set; } = true;
    public int? MaxMinutes { get; set; }
    public int? MaxFuelMinutes { get; set; }
    public bool IsReusable { get; set; }
    public List<string> AcceptedFuelNames { get; set; } = new();
    public int? MinutesPerFuelUnit { get; set; }
    public string Recovery { get; set; } = "none";
    public string? PlayerDescription { get; set; }
    public string? DmDescription { get; set; }
    public string? Scenario { get; set; }
    public string? IconUrl { get; set; }
}

public class PlayerEffectDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int RemainingTurns { get; set; }
}

public class CharacterDto
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ItemDto> Items { get; set; } = new();
    public List<PlayerEffectDto> Effects { get; set; } = new();
}

public class LootItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? PlayerDescription { get; set; }
    public string? DmDescription { get; set; }
    public bool Claimable { get; set; } = true;
    public string? Scenario { get; set; }
    public string? IconUrl { get; set; }
}

public class EncounterDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<LootItemDto> Items { get; set; } = new();
    public string? Scenario { get; set; }
}

public class PartyDto
{
    public List<LootItemDto> LootBox { get; set; } = new();
    public List<ItemDto> Inventory { get; set; } = new();
}

public class TurnStateDto
{
    public int CurrentTurn { get; set; }
    public TimeMode TimeMode { get; set; } = Domain.TimeMode.Dungeon;
    public string? ActiveScenario { get; set; }
    public string? ActiveScenarioTheme { get; set; }
}

public class ScenarioDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Theme { get; set; }
}

public class ActionLogEntryDto
{
    public string UserId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}
