namespace DungeonRunner.Domain;

/// <summary>
/// A named scenario ("Star Trek", "Christmas one-shot"). The Id is the tag
/// value stored on Items/LootItems/Templates/Encounters — immutable once
/// created, since renaming it would orphan every tagged instance. Name is
/// display-only and freely editable.
/// </summary>
public class Scenario
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Optional CSS theme key that rides with this scenario when it's started.</summary>
    public string? Theme { get; set; }
}
