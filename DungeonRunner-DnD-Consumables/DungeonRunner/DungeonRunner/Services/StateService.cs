using System.Text.Json;
using System.Text.Json.Serialization;
using DungeonRunner.Domain;

namespace DungeonRunner.Services;

public class StateService
{
    private readonly string _dataRoot = Path.Combine(AppContext.BaseDirectory, "Data");

    public string IconsDir => Path.Combine(_dataRoot, "icons");

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        }
    };

    public Party Party { get; private set; } = new();
    public TurnState TurnState { get; private set; } = new();
    public Dictionary<string, Character> Users { get; private set; } = new();
    public List<ItemTemplate> ItemCatalog { get; private set; } = new();
    public List<Encounter> Encounters { get; private set; } = new();
    public List<Scenario> Scenarios { get; private set; } = new();

    // Session-only
    public List<ActionLogEntry> ActionLog { get; } = new();

    public void LoadAll()
    {
        Directory.CreateDirectory(_dataRoot);
        Directory.CreateDirectory(Path.Combine(_dataRoot, "users"));
        Directory.CreateDirectory(IconsDir);

        Party = Load<Party>(Path.Combine(_dataRoot, "party.json")) ?? new Party();
        TurnState = Load<TurnState>(Path.Combine(_dataRoot, "turnstate.json")) ?? new TurnState();
        ItemCatalog = Load<List<ItemTemplate>>(Path.Combine(_dataRoot, "catalog.json")) ?? new();
        Encounters = Load<List<Encounter>>(Path.Combine(_dataRoot, "encounters.json")) ?? new();
        Scenarios = Load<List<Scenario>>(Path.Combine(_dataRoot, "scenarios.json")) ?? new();

        Users = new Dictionary<string, Character>();
        foreach (var file in Directory.GetFiles(Path.Combine(_dataRoot, "users"), "*.json"))
        {
            var userId = Path.GetFileNameWithoutExtension(file);
            var character = Load<Character>(file);
            if (character != null)
            {
                character.UserId = userId;
                Users[userId] = character;
            }
        }

        Console.WriteLine($"[StateService] Loaded {Users.Count} characters, {ItemCatalog.Count} catalog items, {Encounters.Count} encounters.");
    }

    public async Task SaveAll()
    {
        Directory.CreateDirectory(_dataRoot);
        Directory.CreateDirectory(Path.Combine(_dataRoot, "users"));

        await SaveAsync(Path.Combine(_dataRoot, "party.json"), Party);
        await SaveAsync(Path.Combine(_dataRoot, "turnstate.json"), TurnState);
        await SaveAsync(Path.Combine(_dataRoot, "catalog.json"), ItemCatalog);
        await SaveAsync(Path.Combine(_dataRoot, "encounters.json"), Encounters);
        await SaveAsync(Path.Combine(_dataRoot, "scenarios.json"), Scenarios);

        foreach (var kvp in Users)
            await SaveAsync(Path.Combine(_dataRoot, "users", $"{kvp.Key}.json"), kvp.Value);
    }

    public ActionLogEntry Log(string userId, string characterName, string action)
    {
        var entry = new ActionLogEntry
        {
            UserId = userId,
            CharacterName = characterName,
            Action = action,
            Timestamp = DateTime.Now
        };
        ActionLog.Add(entry);
        if (ActionLog.Count > 200) ActionLog.RemoveAt(0);
        return entry;
    }

    // Resolves an icon URL by item name, checking both Name and DisplayName
    // on every catalog template. Used by DtoMapper to decorate item DTOs.
    public string? IconUrlFor(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return null;
        foreach (var t in ItemCatalog)
        {
            if (t.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
                (t.DisplayName != null && t.DisplayName.Equals(itemName, StringComparison.OrdinalIgnoreCase)))
                return t.IconUrl;
        }
        return null;
    }

    private T? Load<T>(string path)
    {
        if (!File.Exists(path)) return default;
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), _jsonOptions);
    }

    private async Task SaveAsync<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value, _jsonOptions));
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }
}
