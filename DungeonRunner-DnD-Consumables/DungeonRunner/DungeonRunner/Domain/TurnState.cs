namespace DungeonRunner.Domain;

public class TurnState
{
    public int CurrentTurn { get; set; } = 1;
    public TimeMode TimeMode { get; set; } = TimeMode.Dungeon;

    // Null = normal play. When set (e.g. "startrek"), non-DM views are filtered
    // to items carrying the same tag, and the rule engine only decays matching
    // items. Ending the scenario purges tagged instances from the world.
    public string? ActiveScenario { get; set; }

    // Optional CSS theme key broadcast alongside ActiveScenario. When set,
    // every client forces data-theme on the document root to this value,
    // regardless of their personal theme preference. Cleared when the
    // scenario ends. The theme name is just a string — clients map it to a
    // CSS block in index.css (e.g. "christmas", "lcars").
    public string? ActiveScenarioTheme { get; set; }
}
