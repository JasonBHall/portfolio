namespace DungeonRunner.Domain;

public enum ItemType
{
    Consumable,  // arrows, potions, torches — quantity depletes
    Renewable,   // spell slots, abilities — recharge on rest
    Equipment    // lanterns, stoves, rifles — durable, never auto-removed
}
