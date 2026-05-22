namespace DungeonRunner.Domain;

public class PlayerEffect
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int RemainingTurns { get; set; }
}
