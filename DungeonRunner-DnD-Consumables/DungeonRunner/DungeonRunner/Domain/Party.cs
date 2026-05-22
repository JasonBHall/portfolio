namespace DungeonRunner.Domain;

public class Party
{
    // DM drops loot here. Players claim from here to personal or party inventory.
    public List<LootItem> LootBox { get; set; } = new();

    // Shared party inventory. Items here are owned collectively.
    // Claimable items can be moved to a player's personal inventory.
    public List<Item> Inventory { get; set; } = new();
}
