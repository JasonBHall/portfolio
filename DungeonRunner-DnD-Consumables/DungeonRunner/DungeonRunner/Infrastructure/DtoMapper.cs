using DungeonRunner.Domain;
using DungeonRunner.DTOs;

namespace DungeonRunner.Infrastructure;

public static class DtoMapper
{
    // ---------------------------------------------------------
    // Domain → DTO
    //
    // The `resolveIcon` delegate looks up a template IconUrl by item name
    // (case-insensitive). `activeScenario` is used only when isDm == false:
    // items whose Scenario doesn't equal activeScenario are filtered out,
    // so untagged items are hidden during a one-shot and tagged items are
    // hidden during normal play.
    // ---------------------------------------------------------
    public static CharacterDto ToCharacterDto(
        Character c,
        bool isDm = false,
        string? activeScenario = null,
        Func<string, string?>? resolveIcon = null) => new()
    {
        UserId = c.UserId,
        Name = c.Name,
        Items = c.Items
            .Where(i => isDm || i.Scenario == activeScenario)
            .Select(i => ToItemDto(i, isDm, resolveIcon))
            .ToList(),
        Effects = c.Effects.Select(ToEffectDto).ToList()
    };

    public static ItemDto ToItemDto(
        Item i,
        bool isDm = false,
        Func<string, string?>? resolveIcon = null) => new()
    {
        Id = i.Id,
        Name = i.Name,
        DisplayName = i.DisplayName,
        DisplayNamePlural = i.DisplayNamePlural,
        Category = i.Category,
        Verb = i.Verb,
        VerbOff = i.VerbOff,
        Type = i.Type,
        Quantity = i.Quantity,
        Charges = i.Charges,
        MaxQuantity = i.MaxQuantity,
        MinConsumption = i.MinConsumption,
        RecoveryChance = i.RecoveryChance,
        Claimable = i.Claimable,
        CanGive = i.CanGive,
        IsActive = i.IsActive,
        MaxMinutes = i.MaxMinutes,
        RemainingMinutes = i.RemainingMinutes,
        MaxFuelMinutes = i.MaxFuelMinutes,
        IsReusable = i.IsReusable,
        AcceptedFuelNames = i.AcceptedFuelNames,
        MinutesPerFuelUnit = i.MinutesPerFuelUnit,
        Recovery = i.Recovery,
        PlayerDescription = i.PlayerDescription,
        DmDescription = isDm ? i.DmDescription : (i.DmDescriptionRevealed ? i.DmDescription : null),
        DmDescriptionRevealed = i.DmDescriptionRevealed,
        IsPinned = i.IsPinned,
        Scenario = i.Scenario,
        IconUrl = resolveIcon?.Invoke(i.Name) ?? (i.DisplayName != null ? resolveIcon?.Invoke(i.DisplayName) : null),
    };

    public static PlayerEffectDto ToEffectDto(PlayerEffect e) => new()
    {
        Name = e.Name,
        Description = e.Description,
        RemainingTurns = e.RemainingTurns
    };

    public static PartyDto ToPartyDto(
        Party p,
        bool isDm = false,
        string? activeScenario = null,
        Func<string, string?>? resolveIcon = null) => new()
    {
        LootBox = p.LootBox
            .Where(l => isDm || l.Scenario == activeScenario)
            .Select(l => ToLootItemDto(l, isDm, resolveIcon))
            .ToList(),
        Inventory = p.Inventory
            .Where(i => isDm || i.Scenario == activeScenario)
            .Select(i => ToItemDto(i, isDm, resolveIcon))
            .ToList()
    };

    public static LootItemDto ToLootItemDto(
        LootItem l,
        bool isDm = false,
        Func<string, string?>? resolveIcon = null) => new()
    {
        Id = l.Id,
        Name = l.Name,
        Quantity = l.Quantity,
        PlayerDescription = l.PlayerDescription,
        DmDescription = isDm ? l.DmDescription : null,
        Claimable = l.Claimable,
        Scenario = l.Scenario,
        IconUrl = resolveIcon?.Invoke(l.Name),
    };

    public static EncounterDto ToEncounterDto(
        Encounter e,
        Func<string, string?>? resolveIcon = null) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Items = e.Items.Select(l => ToLootItemDto(l, isDm: true, resolveIcon)).ToList(),
        Scenario = e.Scenario,
    };

    public static TurnStateDto ToTurnStateDto(TurnState t) => new()
    {
        CurrentTurn = t.CurrentTurn,
        TimeMode = t.TimeMode,
        ActiveScenario = t.ActiveScenario,
        ActiveScenarioTheme = t.ActiveScenarioTheme,
    };

    public static ActionLogEntryDto ToActionLogEntryDto(ActionLogEntry e) => new()
    {
        UserId = e.UserId,
        CharacterName = e.CharacterName,
        Action = e.Action,
        Timestamp = e.Timestamp.ToString("HH:mm:ss")
    };

    public static ScenarioDto ToScenarioDto(Scenario s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Theme = s.Theme,
    };

    // ---------------------------------------------------------
    // DTO → Domain (catalog round-trip)
    //
    // Replaces the old GameHub.ToTemplate(CatalogItemRequest). If Type or
    // Recovery don't parse we fall back to sensible defaults rather than
    // throwing, so a malformed client payload can't crash the hub method.
    // ---------------------------------------------------------
    public static ItemTemplate FromItemTemplateDto(ItemTemplateDto r) => new()
    {
        Id = string.IsNullOrWhiteSpace(r.Id) ? Guid.NewGuid().ToString() : r.Id!,
        Name = r.Name,
        DisplayName = r.DisplayName,
        DisplayNamePlural = r.DisplayNamePlural,
        Category = r.Category,
        Verb = r.Verb ?? string.Empty,
        VerbOff = r.VerbOff,
        Type = Enum.TryParse<ItemType>(r.Type, ignoreCase: true, out var t) ? t : ItemType.Consumable,
        MaxQuantity = r.MaxQuantity,
        MinConsumption = r.MinConsumption,
        RecoveryChance = r.RecoveryChance,
        Claimable = r.Claimable,
        CanGive = r.CanGive,
        MaxMinutes = r.MaxMinutes,
        MaxFuelMinutes = r.MaxFuelMinutes,
        IsReusable = r.IsReusable,
        AcceptedFuelNames = new List<string>(r.AcceptedFuelNames),
        MinutesPerFuelUnit = r.MinutesPerFuelUnit,
        Recovery = Enum.TryParse<RecoveryType>(r.Recovery, ignoreCase: true, out var rec) ? rec : RecoveryType.None,
        PlayerDescription = r.PlayerDescription,
        DmDescription = r.DmDescription,
        Scenario = r.Scenario,
        IconUrl = r.IconUrl,
    };
}
