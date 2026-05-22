namespace DungeonRunner.Domain;

public class ActionLogEntry
{
    public string UserId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
