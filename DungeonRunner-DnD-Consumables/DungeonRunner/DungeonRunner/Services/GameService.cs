using DungeonRunner.Domain;

namespace DungeonRunner.Services;

// Unified return type for state-mutating service calls. Carries just enough
// information for GameHub to format an action-log line without having to
// re-resolve entities. Any unused field stays empty.
public readonly record struct ItemActionResult(
    bool Ok,
    string Label = "",       // primary item display name
    string Verb = "",        // action verb (Use/Light/Snuff) — raw, hub may conjugate
    string Secondary = ""    // recipient name / fuel name / character name / etc.
);

public class GameService
{
    private readonly StateService _state;

    public GameService(StateService state) => _state = state;

    private static ItemActionResult Fail => new(false);

    // ---------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------
    private ItemTemplate? FindTemplateById(string id) =>
        _state.ItemCatalog.FirstOrDefault(t => t.Id == id);

    private ItemTemplate? FindTemplateByName(string name) =>
        _state.ItemCatalog.FirstOrDefault(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            (t.DisplayName != null && t.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)));

    private static Item? FindItem(Character c, string itemId) =>
        c.Items.FirstOrDefault(i => i.Id == itemId);

    private static Item? FindPartyItem(Party p, string itemId) =>
        p.Inventory.FirstOrDefault(i => i.Id == itemId);

    private static string Label(Item i)     => i.DisplayName ?? i.Name;
    private static string Label(LootItem l) => l.Name;

    private static void TryRemoveDepletedConsumable(List<Item> items, Item item)
    {
        if (item.Type == ItemType.Consumable && item.Quantity <= 0 &&
            (item.RecoveryChance == null || item.RecoveryChance <= 0))
            items.Remove(item);
    }

    private static bool FuelMatches(Item fuelItem, Item targetItem) =>
        targetItem.AcceptedFuelNames.Any(accepted =>
            fuelItem.Name.Equals(accepted, StringComparison.OrdinalIgnoreCase) ||
            (fuelItem.DisplayName != null &&
             fuelItem.DisplayName.Equals(accepted, StringComparison.OrdinalIgnoreCase)));

    // ---------------------------------------------------------
    // PLAYER MANAGEMENT
    // ---------------------------------------------------------
    public bool CreatePlayer(string userId, string characterName)
    {
        if (_state.Users.ContainsKey(userId)) return false;
        _state.Users[userId] = new Character
        {
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(characterName) ? userId : characterName
        };
        return true;
    }

    // ---------------------------------------------------------
    // CATALOG
    // ---------------------------------------------------------
    public void CreateCatalogItem(ItemTemplate t)
    {
        if (string.IsNullOrWhiteSpace(t.Id)) t.Id = Guid.NewGuid().ToString();
        _state.ItemCatalog.Add(t);
    }

    public bool UpdateCatalogItem(ItemTemplate t)
    {
        var e = _state.ItemCatalog.FirstOrDefault(x => x.Id == t.Id);
        if (e == null) return false;
        e.Name = t.Name; e.DisplayName = t.DisplayName; e.DisplayNamePlural = t.DisplayNamePlural;
        e.Category = t.Category; e.Verb = t.Verb; e.VerbOff = t.VerbOff;
        e.Type = t.Type; e.MaxQuantity = t.MaxQuantity; e.MinConsumption = t.MinConsumption;
        e.RecoveryChance = t.RecoveryChance; e.Claimable = t.Claimable; e.CanGive = t.CanGive;
        e.MaxMinutes = t.MaxMinutes; e.MaxFuelMinutes = t.MaxFuelMinutes;
        e.IsReusable = t.IsReusable;
        e.AcceptedFuelNames = new List<string>(t.AcceptedFuelNames);
        e.MinutesPerFuelUnit = t.MinutesPerFuelUnit;
        e.Recovery = t.Recovery;
        e.PlayerDescription = t.PlayerDescription; e.DmDescription = t.DmDescription;
        e.Scenario = t.Scenario;
        // IconUrl is NOT overwritten here — uploads go through SaveItemIcon.
        return true;
    }

    public bool DeleteCatalogItem(string id)
    {
        var e = _state.ItemCatalog.FirstOrDefault(t => t.Id == id);
        if (e == null) return false;
        RemoveIconFile(e);
        _state.ItemCatalog.Remove(e);
        return true;
    }

    // ---------------------------------------------------------
    // ICONS
    // ---------------------------------------------------------
    // Writes base64-decoded PNG bytes to Data/icons/{templateId}.png and
    // stamps a cache-busting IconUrl on the template. Caller enforces upload
    // caps at the hub boundary.
    public bool SaveItemIcon(string templateId, byte[] pngBytes)
    {
        var t = FindTemplateById(templateId);
        if (t == null) return false;
        var path = Path.Combine(_state.IconsDir, $"{templateId}.png");
        File.WriteAllBytes(path, pngBytes);
        t.IconUrl = $"/icons/{templateId}.png?v={DateTime.UtcNow.Ticks}";
        return true;
    }

    public bool RemoveItemIcon(string templateId)
    {
        var t = FindTemplateById(templateId);
        if (t == null) return false;
        RemoveIconFile(t);
        t.IconUrl = null;
        return true;
    }

    private void RemoveIconFile(ItemTemplate t)
    {
        var path = Path.Combine(_state.IconsDir, $"{t.Id}.png");
        if (File.Exists(path)) File.Delete(path);
    }

    // ---------------------------------------------------------
    // SCENARIO CONTROL
    //
    // Starting a scenario flips the visibility filter. Ending one additionally
    // purges all tagged instances from the world (character inventories, loot
    // box, party inventory, encounter loot). Templates are never touched — so
    // you can reuse the same phaser rifle catalog entry next session.
    // ---------------------------------------------------------
    // ---------------------------------------------------------
    // SCENARIO LIFECYCLE
    //
    // Starting a scenario flips the visibility filter. The theme (if any)
    // is read from the Scenario record stored in state — no longer passed
    // per-start. Ending additionally purges all tagged instances; templates
    // are never touched so you can rerun the same one-shot later.
    // ---------------------------------------------------------
    public bool StartScenario(string scenarioId)
    {
        var s = _state.Scenarios.FirstOrDefault(x => x.Id == scenarioId);
        if (s == null) return false;
        _state.TurnState.ActiveScenario = s.Id;
        _state.TurnState.ActiveScenarioTheme = string.IsNullOrWhiteSpace(s.Theme) ? null : s.Theme;
        return true;
    }

    public string? EndScenario()
    {
        var ended = _state.TurnState.ActiveScenario;
        if (ended == null) return null;
        PurgeScenarioInstances(ended);
        _state.TurnState.ActiveScenario = null;
        _state.TurnState.ActiveScenarioTheme = null;
        return ended;
    }

    private void PurgeScenarioInstances(string scenario)
    {
        foreach (var c in _state.Users.Values)
            c.Items.RemoveAll(i => i.Scenario == scenario);

        _state.Party.LootBox.RemoveAll(l => l.Scenario == scenario);
        _state.Party.Inventory.RemoveAll(i => i.Scenario == scenario);

        foreach (var enc in _state.Encounters)
            enc.Items.RemoveAll(i => i.Scenario == scenario);
    }

    // ---------------------------------------------------------
    // SCENARIO CATALOG (CRUD)
    //
    // Ids are immutable — renaming would orphan every tagged instance.
    // Deletion is blocked while a scenario is either active or still has
    // any items/templates/encounters tagged to it. The caller handles the
    // "why can't I delete this?" messaging using CountItemsInScenario.
    // ---------------------------------------------------------
    public Scenario? CreateScenario(string id, string name, string? theme)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        // No whitespace in the id (stored in data; used in URLs/JSON keys).
        if (id.Any(char.IsWhiteSpace)) return null;
        if (_state.Scenarios.Any(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) return null;
        var s = new Scenario
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? id : name,
            Theme = string.IsNullOrWhiteSpace(theme) ? null : theme,
        };
        _state.Scenarios.Add(s);
        return s;
    }

    /// <summary>Update name/theme. Id is immutable.</summary>
    public bool UpdateScenario(string id, string name, string? theme)
    {
        var s = _state.Scenarios.FirstOrDefault(x => x.Id == id);
        if (s == null) return false;
        if (!string.IsNullOrWhiteSpace(name)) s.Name = name;
        s.Theme = string.IsNullOrWhiteSpace(theme) ? null : theme;
        // If this scenario is currently active, propagate the theme change live.
        if (_state.TurnState.ActiveScenario == id)
            _state.TurnState.ActiveScenarioTheme = s.Theme;
        return true;
    }

    /// <summary>Returns delete result: true=deleted, false=blocked (active or in use).</summary>
    public bool DeleteScenario(string id, out int inUseCount)
    {
        inUseCount = 0;
        if (_state.TurnState.ActiveScenario == id) return false;
        inUseCount = CountItemsInScenario(id);
        if (inUseCount > 0) return false;
        var removed = _state.Scenarios.RemoveAll(s => s.Id == id);
        return removed > 0;
    }

    /// <summary>Count of instances tagged to a scenario across the whole world.</summary>
    public int CountItemsInScenario(string id)
    {
        var count = 0;
        foreach (var c in _state.Users.Values)
            count += c.Items.Count(i => i.Scenario == id);
        count += _state.Party.LootBox.Count(l => l.Scenario == id);
        count += _state.Party.Inventory.Count(i => i.Scenario == id);
        foreach (var enc in _state.Encounters)
            count += enc.Items.Count(i => i.Scenario == id);
        count += _state.ItemCatalog.Count(t => t.Scenario == id);
        return count;
    }

    // ---------------------------------------------------------
    // ENCOUNTERS
    // ---------------------------------------------------------
    public Encounter CreateEncounter(string name, string? scenario = null)
    {
        var enc = new Encounter { Name = name, Scenario = scenario };
        _state.Encounters.Add(enc);
        return enc;
    }

    public bool AddItemToEncounter(string encounterId, string name, int quantity)
    {
        var enc = _state.Encounters.FirstOrDefault(e => e.Id == encounterId);
        if (enc == null) return false;
        var t = FindTemplateByName(name);   // propagate descriptions & scenario if we know it
        enc.Items.Add(new LootItem
        {
            Name = name,
            Quantity = quantity,
            Claimable = t?.Claimable ?? true,
            PlayerDescription = t?.PlayerDescription,
            DmDescription = t?.DmDescription,
            Scenario = t?.Scenario ?? enc.Scenario,
        });
        return true;
    }

    public bool AddCatalogItemToEncounter(string encounterId, string templateId, int quantity)
    {
        var enc  = _state.Encounters.FirstOrDefault(e => e.Id == encounterId);
        var t = FindTemplateById(templateId);
        if (enc == null || t == null) return false;
        enc.Items.Add(new LootItem
        {
            Name = t.DisplayName ?? t.Name,
            Quantity = quantity,
            Claimable = t.Claimable,
            PlayerDescription = t.PlayerDescription,
            DmDescription = t.DmDescription,
            Scenario = t.Scenario ?? enc.Scenario,
        });
        return true;
    }

    public bool EditEncounterItem(string encounterId, string lootItemId, int? quantity, string? playerDesc, string? dmDesc)
    {
        var enc = _state.Encounters.FirstOrDefault(e => e.Id == encounterId);
        if (enc == null) return false;
        var item = enc.Items.FirstOrDefault(i => i.Id == lootItemId);
        if (item == null) return false;
        if (quantity.HasValue) item.Quantity = Math.Max(0, quantity.Value);
        item.PlayerDescription = playerDesc;
        item.DmDescription = dmDesc;
        if (item.Quantity <= 0) enc.Items.Remove(item);
        return true;
    }

    public bool RemoveItemFromEncounter(string encounterId, string lootItemId)
    {
        var enc = _state.Encounters.FirstOrDefault(e => e.Id == encounterId);
        if (enc == null) return false;
        var item = enc.Items.FirstOrDefault(i => i.Id == lootItemId);
        if (item == null) return false;
        enc.Items.Remove(item);
        return true;
    }

    // Returns the encounter name on success so the hub can log it.
    public ItemActionResult PushEncounterToLootBox(string encounterId)
    {
        var enc = _state.Encounters.FirstOrDefault(e => e.Id == encounterId);
        if (enc == null || enc.Items.Count == 0) return Fail;
        foreach (var item in enc.Items)
            MergeLootItemIntoBox(item.Name, item.Quantity, item.Claimable,
                item.PlayerDescription, item.DmDescription, item.Scenario);
        enc.Items.Clear();
        return new(true, Label: enc.Name);
    }

    public ItemActionResult PushSingleEncounterItemToLootBox(string encounterId, string lootItemId)
    {
        var enc = _state.Encounters.FirstOrDefault(e => e.Id == encounterId);
        if (enc == null) return Fail;
        var item = enc.Items.FirstOrDefault(i => i.Id == lootItemId);
        if (item == null) return Fail;
        var label = Label(item);
        MergeLootItemIntoBox(item.Name, item.Quantity, item.Claimable,
            item.PlayerDescription, item.DmDescription, item.Scenario);
        enc.Items.Remove(item);
        return new(true, Label: label);
    }

    public bool DeleteEncounter(string encounterId)
    {
        var enc = _state.Encounters.FirstOrDefault(e => e.Id == encounterId);
        if (enc == null) return false;
        _state.Encounters.Remove(enc);
        return true;
    }

    // ---------------------------------------------------------
    // LOOT BOX
    //
    // All add-paths now funnel through MergeLootItemIntoBox so behavior is
    // consistent — adding "Torch x1" twice produces one stack of 2.
    // Merging is keyed on (name, scenario) so one-shot loot never fuses with
    // the normal-world stack of the same name.
    // ---------------------------------------------------------
    public void AddToLootBox(string name, int quantity)
    {
        var t = FindTemplateByName(name);
        MergeLootItemIntoBox(name, quantity,
            claimable: t?.Claimable ?? true,
            playerDesc: t?.PlayerDescription,
            dmDesc: t?.DmDescription,
            scenario: t?.Scenario);
    }

    public void AddToLootBoxFromCatalog(string templateId, int quantity)
    {
        var t = FindTemplateById(templateId);
        if (t == null) return;
        MergeLootItemIntoBox(t.DisplayName ?? t.Name, quantity, t.Claimable,
            t.PlayerDescription, t.DmDescription, t.Scenario);
    }

    public void ClearLootBox() => _state.Party.LootBox.Clear();

    public bool EditLootItem(string lootItemId, int? quantity, string? playerDesc, string? dmDesc)
    {
        var l = _state.Party.LootBox.FirstOrDefault(x => x.Id == lootItemId);
        if (l == null) return false;
        if (quantity.HasValue) l.Quantity = Math.Max(0, quantity.Value);
        l.PlayerDescription = playerDesc; l.DmDescription = dmDesc;
        if (l.Quantity <= 0) _state.Party.LootBox.Remove(l);
        return true;
    }

    public ItemActionResult DeleteLootItem(string lootItemId)
    {
        var l = _state.Party.LootBox.FirstOrDefault(x => x.Id == lootItemId);
        if (l == null) return Fail;
        var label = Label(l);
        _state.Party.LootBox.Remove(l);
        return new(true, Label: label);
    }

    public ItemActionResult ClaimLootToPlayer(string userId, string lootItemId, int quantity)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var loot = _state.Party.LootBox.FirstOrDefault(l => l.Id == lootItemId);
        if (loot == null || !loot.Claimable || loot.Quantity < quantity) return Fail;
        var label = Label(loot);
        loot.Quantity -= quantity;
        if (loot.Quantity <= 0) _state.Party.LootBox.Remove(loot);
        AddItemToCharacter(c, loot.Name, quantity, loot.Scenario);
        return new(true, Label: label);
    }

    public ItemActionResult ClaimLootToParty(string lootItemId, int quantity)
    {
        var loot = _state.Party.LootBox.FirstOrDefault(l => l.Id == lootItemId);
        if (loot == null || loot.Quantity < quantity) return Fail;
        var label = Label(loot);
        loot.Quantity -= quantity;
        if (loot.Quantity <= 0) _state.Party.LootBox.Remove(loot);
        AddToPartyInventoryByName(loot.Name, quantity, loot.PlayerDescription, loot.DmDescription, loot.Scenario);
        return new(true, Label: label);
    }

    private void MergeLootItemIntoBox(string name, int quantity, bool claimable, string? playerDesc, string? dmDesc, string? scenario)
    {
        var existing = _state.Party.LootBox.FirstOrDefault(l =>
            l.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            l.Scenario == scenario);
        if (existing != null)
            existing.Quantity += quantity;
        else
            _state.Party.LootBox.Add(new LootItem
            {
                Name = name, Quantity = quantity, Claimable = claimable,
                PlayerDescription = playerDesc, DmDescription = dmDesc,
                Scenario = scenario,
            });
    }

    // ---------------------------------------------------------
    // PARTY INVENTORY
    // ---------------------------------------------------------
    public void AddToPartyInventory(string name, int quantity)
    {
        var t = FindTemplateByName(name);
        AddToPartyInventoryByName(name, quantity, t?.PlayerDescription, t?.DmDescription, t?.Scenario);
    }

    public void AddToPartyInventoryFromCatalog(string templateId, int quantity)
    {
        var t = FindTemplateById(templateId);
        if (t == null) return;
        AddToPartyInventoryByName(t.DisplayName ?? t.Name, quantity, t.PlayerDescription, t.DmDescription, t.Scenario);
    }

    public bool EditPartyItem(string itemId, int? quantity, string? playerDesc, string? dmDesc)
    {
        var item = FindPartyItem(_state.Party, itemId);
        if (item == null) return false;
        if (quantity.HasValue) item.Quantity = Math.Max(0, quantity.Value);
        item.PlayerDescription = playerDesc; item.DmDescription = dmDesc;
        if (item.Type == ItemType.Consumable && item.Quantity <= 0)
            _state.Party.Inventory.Remove(item);
        return true;
    }

    public ItemActionResult DeletePartyItem(string itemId)
    {
        var item = FindPartyItem(_state.Party, itemId);
        if (item == null) return Fail;
        var label = Label(item);
        _state.Party.Inventory.Remove(item);
        return new(true, Label: label);
    }

    public ItemActionResult ClaimFromPartyToPlayer(string userId, string itemId, int quantity)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var partyItem = FindPartyItem(_state.Party, itemId);
        if (partyItem == null || !partyItem.Claimable || partyItem.Quantity < quantity) return Fail;
        var label = Label(partyItem);
        partyItem.Quantity -= quantity;
        if (partyItem.Quantity <= 0) _state.Party.Inventory.Remove(partyItem);
        AddItemToCharacter(c, partyItem.Name, quantity, partyItem.Scenario);
        return new(true, Label: label);
    }

    public ItemActionResult UsePartyItem(string itemId, int quantity = 1)
    {
        var item = FindPartyItem(_state.Party, itemId);
        if (item == null || item.Quantity < quantity) return Fail;
        var label = Label(item);
        item.Quantity -= quantity;
        TryRemoveDepletedConsumable(_state.Party.Inventory, item);
        return new(true, Label: label);
    }

    // ---------------------------------------------------------
    // PLAYER ITEM ACTIONS
    // ---------------------------------------------------------
    public ItemActionResult UseItem(string userId, string itemId, int quantity = 1)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var item = FindItem(c, itemId);
        if (item == null || item.Type == ItemType.Equipment || item.Quantity < quantity) return Fail;
        var result = new ItemActionResult(true, Label(item), item.Verb);
        item.Quantity -= quantity;
        TryRemoveDepletedConsumable(c.Items, item);
        return result;
    }

    public ItemActionResult RecoverItem(string userId, string itemId)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var item = FindItem(c, itemId);
        if (item == null || item.Type != ItemType.Consumable || !item.RecoveryChance.HasValue) return Fail;
        var label = Label(item);
        if (Random.Shared.NextDouble() < item.RecoveryChance.Value) item.Quantity++;
        if (item.Quantity <= 0) c.Items.Remove(item);
        return new(true, Label: label);
    }

    public ItemActionResult LightItem(string userId, string itemId)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var item = FindItem(c, itemId);
        if (item == null || !item.IsTimed || item.IsActive) return Fail;

        var isReusable = item.IsReusable || item.Type == ItemType.Equipment;
        if (isReusable && item.RemainingMinutes.HasValue && item.RemainingMinutes.Value > 0)
        {
            item.IsActive = true;
        }
        else
        {
            if (item.Type == ItemType.Consumable)
            {
                if (item.Quantity <= 0) return Fail;
                item.Quantity--;
            }
            item.IsActive = true;
            item.RemainingMinutes = item.MaxMinutes ?? 60;
        }
        return new(true, Label(item), item.Verb);
    }

    public ItemActionResult SnuffItem(string userId, string itemId)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var item = FindItem(c, itemId);
        if (item == null || !item.IsTimed || !item.IsActive) return Fail;
        var verb = item.VerbOff ?? "snuffs";
        var label = Label(item);
        item.IsActive = false;
        if (item.Type == ItemType.Consumable && !item.IsReusable)
            item.RemainingMinutes = null;
        return new(true, label, verb);
    }

    // Decrements Charges (not Quantity); Quantity remains the physical item count.
    public ItemActionResult SpendRenewable(string userId, string itemId, int amount = 1)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var item = FindItem(c, itemId);
        if (item == null || item.Type != ItemType.Renewable) return Fail;
        var currentCharges = item.Charges ?? item.MaxQuantity ?? 0;
        if (currentCharges < amount) return Fail;
        item.Charges = Math.Max(0, currentCharges - amount);
        return new(true, Label: Label(item));
    }

    public bool PinItem(string userId, string itemId, bool pinned)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return false;
        var item = FindItem(c, itemId);
        if (item == null) return false;
        item.IsPinned = pinned;
        return true;
    }

    public ItemActionResult GiveItemToParty(string userId, string itemId, int quantity)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var item = FindItem(c, itemId);
        if (item == null || !item.CanGive || item.Quantity < quantity) return Fail;
        var label = Label(item);
        item.Quantity -= quantity;
        TryRemoveDepletedConsumable(c.Items, item);
        AddToPartyInventoryByName(item.Name, quantity, item.PlayerDescription, item.DmDescription, item.Scenario);
        return new(true, Label: label);
    }

    public ItemActionResult GiveItemToPlayer(string fromId, string toId, string itemId, int quantity)
    {
        if (!_state.Users.TryGetValue(fromId, out var from)) return Fail;
        if (!_state.Users.TryGetValue(toId, out var to)) return Fail;
        var item = FindItem(from, itemId);
        if (item == null || !item.CanGive || item.Quantity < quantity) return Fail;
        var label = Label(item);
        item.Quantity -= quantity;
        TryRemoveDepletedConsumable(from.Items, item);
        AddItemToCharacter(to, item.Name, quantity, item.Scenario);
        return new(true, Label: label, Secondary: to.Name);
    }

    public ItemActionResult FuelItem(string userId, string itemId, string fuelItemId, int quantity)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var item = FindItem(c, itemId);
        if (item == null || item.AcceptedFuelNames.Count == 0 || !item.MinutesPerFuelUnit.HasValue)
            return Fail;
        var fuelItem = FindItem(c, fuelItemId);
        if (fuelItem == null || fuelItem.Quantity < quantity) return Fail;
        if (!FuelMatches(fuelItem, item)) return Fail;

        fuelItem.Quantity -= quantity;
        TryRemoveDepletedConsumable(c.Items, fuelItem);
        var newTotal = (item.RemainingMinutes ?? 0) + quantity * item.MinutesPerFuelUnit.Value;
        if (item.MaxFuelMinutes.HasValue) newTotal = Math.Min(newTotal, item.MaxFuelMinutes.Value);
        item.RemainingMinutes = newTotal;
        return new(true, Label: Label(item), Secondary: Label(fuelItem));
    }

    // ---------------------------------------------------------
    // DM ITEM CONTROLS
    // ---------------------------------------------------------
    public ItemActionResult DMDeleteItem(string userId, string itemId)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var item = FindItem(c, itemId);
        if (item == null) return Fail;
        var result = new ItemActionResult(true, Label(item), Secondary: c.Name);
        c.Items.Remove(item);
        return result;
    }

    public ItemActionResult DMAdjustItem(
        string userId, string itemId,
        int? quantity, int? charges, int? remainingMinutes, int? maxQuantity,
        string? playerDesc, string? dmDesc, bool? dmDescRevealed, bool? isPinned,
        string? scenario, bool updateScenario)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var item = FindItem(c, itemId);
        if (item == null) return Fail;

        if (quantity.HasValue)         item.Quantity              = Math.Max(0, quantity.Value);
        if (charges.HasValue)          item.Charges               = Math.Max(0, charges.Value);
        if (remainingMinutes.HasValue) item.RemainingMinutes      = Math.Max(0, remainingMinutes.Value);
        if (maxQuantity.HasValue)      item.MaxQuantity           = Math.Max(0, maxQuantity.Value);
        if (playerDesc != null)        item.PlayerDescription     = playerDesc == "" ? null : playerDesc;
        if (dmDesc != null)            item.DmDescription         = dmDesc == "" ? null : dmDesc;
        if (dmDescRevealed.HasValue)   item.DmDescriptionRevealed = dmDescRevealed.Value;
        if (isPinned.HasValue)         item.IsPinned              = isPinned.Value;
        if (updateScenario)            item.Scenario              = scenario;    // null clears tag

        return new(true, Label(item), Secondary: c.Name);
    }

    public ItemActionResult DMGiveItem(string userId, string templateId, int quantity)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return Fail;
        var t = FindTemplateById(templateId);
        if (t == null) return Fail;
        AddItemToCharacter(c, t.DisplayName ?? t.Name, quantity, t.Scenario);
        return new(true, Label: t.DisplayName ?? t.Name, Secondary: c.Name);
    }

    // ---------------------------------------------------------
    // REST — restores Charges to MaxQuantity for renewables.
    //
    // ShortRest refreshes only Recovery=ShortRest items. LongRest refreshes
    // ShortRest, LongRest, and Daily. Party variants fan out and report
    // which user IDs were touched so the hub can broadcast each.
    // ---------------------------------------------------------
    private static readonly RecoveryType[] ShortRestRecovers =
        { RecoveryType.ShortRest };
    private static readonly RecoveryType[] LongRestRecovers =
        { RecoveryType.ShortRest, RecoveryType.LongRest, RecoveryType.Daily };

    private bool Rest(string userId, RecoveryType[] recovers)
    {
        if (!_state.Users.TryGetValue(userId, out var c)) return false;
        foreach (var item in c.Items.Where(i =>
                     i.Type == ItemType.Renewable &&
                     recovers.Contains(i.Recovery) &&
                     i.MaxQuantity.HasValue))
            item.Charges = item.MaxQuantity;
        return true;
    }

    public bool ShortRest(string userId) => Rest(userId, ShortRestRecovers);
    public bool LongRest(string userId)  => Rest(userId, LongRestRecovers);

    public void ApplyRest(string userId, string type)
    {
        if (type.Equals("short", StringComparison.OrdinalIgnoreCase)) ShortRest(userId);
        else if (type.Equals("long", StringComparison.OrdinalIgnoreCase)) LongRest(userId);
    }

    private List<string> PartyRest(Func<string, bool> rest) =>
        _state.Users.Keys.Where(rest).ToList();

    public List<string> PartyShortRest() => PartyRest(ShortRest);
    public List<string> PartyLongRest()  => PartyRest(LongRest);

    // ---------------------------------------------------------
    // PRIVATE HELPERS
    //
    // Merge lookups key on (name, scenario) — items tagged for a one-shot
    // never fuse with the normal-world stack of the same name.
    // ---------------------------------------------------------
    private void AddItemToCharacter(Character c, string itemName, int quantity, string? scenario)
    {
        var existing = c.Items.FirstOrDefault(i =>
            (i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
             (i.DisplayName != null && i.DisplayName.Equals(itemName, StringComparison.OrdinalIgnoreCase)))
            && i.Scenario == scenario);

        if (existing != null)
        {
            if (existing.Type == ItemType.Renewable && existing.MaxQuantity.HasValue)
            {
                // Adding more wands: increment physical count and charge pool
                existing.Quantity += quantity;
                var addedCharges = quantity * existing.MaxQuantity.Value;
                var maxTotal = existing.Quantity * existing.MaxQuantity.Value;
                existing.Charges = Math.Min((existing.Charges ?? 0) + addedCharges, maxTotal);
            }
            else
            {
                existing.Quantity += quantity;
            }
            return;
        }

        var template = FindTemplateByName(itemName);
        var newItem = template != null
            ? template.CreateInstance(quantity)
            : new Item { Name = itemName, Quantity = quantity, Category = "General" };
        newItem.Scenario = scenario ?? newItem.Scenario;   // caller's tag wins when provided
        c.Items.Add(newItem);
    }

    private void AddToPartyInventoryByName(string name, int quantity, string? playerDesc, string? dmDesc, string? scenario)
    {
        var existing = _state.Party.Inventory.FirstOrDefault(i =>
            (i.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
             (i.DisplayName != null && i.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            && i.Scenario == scenario);

        if (existing != null)
        {
            existing.Quantity += quantity;
            return;
        }

        var t = FindTemplateByName(name);
        var item = t != null
            ? t.CreateInstance(quantity)
            : new Item { Name = name, Quantity = quantity, Category = "General" };
        item.PlayerDescription = playerDesc;
        item.DmDescription = dmDesc;
        item.Scenario = scenario ?? item.Scenario;
        _state.Party.Inventory.Add(item);
    }
}
