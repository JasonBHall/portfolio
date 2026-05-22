using DungeonRunner.Domain;

namespace DungeonRunner.Services;

public enum ThresholdType { Percent50, Percent25, Expired }

public record RuleNotification(
    string UserId,
    string CharacterName,
    string ItemLabel,
    ThresholdType Type
)
{
    public string PlayerMessage => Type switch
    {
        ThresholdType.Percent50 => $"Your {ItemLabel} is at 50%.",
        ThresholdType.Percent25 => $"Your {ItemLabel} is almost spent (25% remaining).",
        ThresholdType.Expired   => $"Your {ItemLabel} has run out.",
        _ => string.Empty
    };

    public string LogMessage => Type switch
    {
        ThresholdType.Percent50 => $"{CharacterName}'s {ItemLabel} is at 50%.",
        ThresholdType.Percent25 => $"{CharacterName}'s {ItemLabel} is at 25%.",
        ThresholdType.Expired   => $"{CharacterName}'s {ItemLabel} has expired.",
        _ => string.Empty
    };
}

public class RuleEngine
{
    public List<RuleNotification> ApplyRules(StateService state)
    {
        var minutes = state.TurnState.TimeMode switch
        {
            TimeMode.Dungeon        => 10,
            TimeMode.UnknownOpenAir => 60,
            TimeMode.OpenAir        => 1440,
            _                       => 0,
        };
        if (minutes == 0) return new List<RuleNotification>();
        return ApplyTimedRules(state, minutes);
    }

    private static List<RuleNotification> ApplyTimedRules(StateService state, int minutesPerTurn)
    {
        var notifications = new List<RuleNotification>();
        var activeScenario = state.TurnState.ActiveScenario;

        foreach (var character in state.Users.Values)
        {
            foreach (var item in character.Items.ToList())
                DecayItem(item, character.Items, minutesPerTurn, activeScenario,
                    character.UserId, character.Name, notifications);

            // Turn-based effect decay — not filtered by scenario; effects are
            // short-lived and mostly cosmetic, and filtering them would make
            // tracking more confusing than it's worth.
            foreach (var effect in character.Effects.ToList())
            {
                effect.RemainingTurns--;
                if (effect.RemainingTurns <= 0) character.Effects.Remove(effect);
            }
        }

        foreach (var item in state.Party.Inventory.ToList())
            DecayItem(item, state.Party.Inventory, minutesPerTurn, activeScenario,
                "party", "Party", notifications);

        return notifications;
    }

    // Single decay path for both character and party-inventory items. Items
    // outside the active scenario scope are skipped entirely — a torch lit
    // before a Star Trek charm is frozen until the charm ends.
    private static void DecayItem(
        Item item,
        List<Item> sourceList,
        int minutesPerTurn,
        string? activeScenario,
        string recipientUserId,
        string recipientName,
        List<RuleNotification> notifications)
    {
        if (item.Scenario != activeScenario) return;
        if (!item.IsTimed || !item.IsActive || !item.RemainingMinutes.HasValue) return;

        var label  = item.DisplayName ?? item.Name;
        var before = item.RemainingMinutes.Value;
        var max    = item.MaxMinutes ?? 0;

        item.RemainingMinutes = Math.Max(0, before - minutesPerTurn);
        var after = item.RemainingMinutes.Value;

        if (after <= 0)
        {
            item.IsActive = false;
            // Equipment stays in inventory at 0; consumables are removed.
            if (item.Type == ItemType.Consumable) sourceList.Remove(item);
            notifications.Add(new RuleNotification(recipientUserId, recipientName, label, ThresholdType.Expired));
            return;
        }

        if (max <= 0) return;
        var pctBefore = (double)before / max * 100;
        var pctAfter  = (double)after  / max * 100;

        if (pctBefore > 50 && pctAfter <= 50)
            notifications.Add(new RuleNotification(recipientUserId, recipientName, label, ThresholdType.Percent50));
        else if (pctBefore > 25 && pctAfter <= 25)
            notifications.Add(new RuleNotification(recipientUserId, recipientName, label, ThresholdType.Percent25));
    }
}
