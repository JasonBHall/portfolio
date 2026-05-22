namespace DungeonRunner.Domain;

public class Character
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<Item> Items { get; set; } = new();
    public List<PlayerEffect> Effects { get; set; } = new();
}
