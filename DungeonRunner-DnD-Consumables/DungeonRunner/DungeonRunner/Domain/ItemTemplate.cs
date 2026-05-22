namespace DungeonRunner.Domain;

public class ItemTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? DisplayNamePlural { get; set; }
    public string Category { get; set; } = "General";
    public string Verb { get; set; } = string.Empty;
    public string? VerbOff { get; set; }

    public ItemType Type { get; set; } = ItemType.Consumable;

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
    public RecoveryType Recovery { get; set; } = RecoveryType.None;
    public string? PlayerDescription { get; set; }
    public string? DmDescription { get; set; }

    // Scenario this template belongs to. Instances created from this template
    // inherit the tag.
    public string? Scenario { get; set; }

    // Relative URL to the template's icon on disk (e.g. "/icons/{id}.png?v=...").
    // Null when no icon has been uploaded.
    public string? IconUrl { get; set; }

    public Item CreateInstance(int quantity) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = Name,
        DisplayName = DisplayName,
        DisplayNamePlural = DisplayNamePlural,
        Category = Category,
        Verb = Verb,
        VerbOff = VerbOff,
        Type = Type,
        Quantity = quantity,
        // Renewables start fully charged. Non-renewables leave Charges null.
        Charges = Type == ItemType.Renewable ? MaxQuantity : null,
        MaxQuantity = MaxQuantity,
        MinConsumption = MinConsumption,
        RecoveryChance = RecoveryChance,
        Claimable = Claimable,
        CanGive = CanGive,
        MaxMinutes = MaxMinutes,
        MaxFuelMinutes = MaxFuelMinutes,
        RemainingMinutes = null,
        IsActive = false,
        IsReusable = IsReusable || Type == ItemType.Equipment,
        AcceptedFuelNames = new List<string>(AcceptedFuelNames),
        MinutesPerFuelUnit = MinutesPerFuelUnit,
        Recovery = Recovery,
        PlayerDescription = PlayerDescription,
        DmDescription = DmDescription,
        DmDescriptionRevealed = false,
        IsPinned = false,
        Scenario = Scenario,
    };
}
