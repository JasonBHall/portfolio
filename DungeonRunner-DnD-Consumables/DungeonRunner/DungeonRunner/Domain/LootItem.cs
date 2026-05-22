namespace DungeonRunner.Domain;

public class LootItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? PlayerDescription { get; set; }
    public string? DmDescription { get; set; }

    // When false the item cannot be claimed by a player to their personal inventory.
    // On the loot box UI this renders as "→ Party Inventory" instead of "Claim".
    public bool Claimable { get; set; } = true;

    // Scenario tag propagated from the originating template. Filtered the same
    // way as Item.Scenario for non-DM views.
    public string? Scenario { get; set; }
}
