namespace DungeonRunner.Domain;

public class Item
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? DisplayNamePlural { get; set; }
    public string Category { get; set; } = "General";

    // Primary action verb ("light", "cast", "shoot"). Empty = no action button (compass, map, etc.)
    public string Verb { get; set; } = string.Empty;
    public string? VerbOff { get; set; }

    public ItemType Type { get; set; } = ItemType.Consumable;

    // Physical item count. For renewables this is the number of items (wands, scrolls).
    // For consumables this is units remaining.
    public int Quantity { get; set; }

    // Renewable only: current charges available (the spendable resource).
    // Distinct from Quantity — spending reduces Charges, not Quantity.
    // Rest restores Charges to MaxQuantity.
    public int? Charges { get; set; }

    // Renewable: max charges. Consumable/Equipment: unused.
    public int? MaxQuantity { get; set; }

    public double? MinConsumption { get; set; }
    public double? RecoveryChance { get; set; }
    public bool Claimable { get; set; } = true;
    public bool CanGive { get; set; } = true;

    // Active state and duration (timed consumables + equipment)
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

    // Optional scenario tag ("startrek", "stargate", "christmas").
    // When the TurnState's ActiveScenario is set, only items with a matching
    // tag are visible to players or decayed by the rule engine.
    public string? Scenario { get; set; }

    // Equipment OR a consumable with a duration; used by decay rules.
    public bool IsTimed =>
        Type == ItemType.Equipment ||
        (Type == ItemType.Consumable && MaxMinutes.HasValue);
}
