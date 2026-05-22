namespace DungeonRunner.Domain;

public class Encounter
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public List<LootItem> Items { get; set; } = new();

    // Optional scenario tag — organizational hint for the DM. Not filtered on
    // the wire (encounters are DM-only anyway); DM UI can group by this.
    public string? Scenario { get; set; }
}
