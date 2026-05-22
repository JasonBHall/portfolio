using DungeonRunner.Services;

namespace DungeonRunner.Services;

public class TurnEngine
{
    private readonly StateService _state;
    private readonly RuleEngine _rules;

    public TurnEngine(StateService state, RuleEngine rules)
    {
        _state = state;
        _rules = rules;
    }

    public async Task<List<RuleNotification>> AdvanceTurn()
    {
        _state.TurnState.CurrentTurn++;
        var notifications = _rules.ApplyRules(_state);
        await _state.SaveAll();
        return notifications;
    }
}
