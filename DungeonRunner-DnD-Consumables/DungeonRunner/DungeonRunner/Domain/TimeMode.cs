namespace DungeonRunner.Domain;

public enum TimeMode
{
    OpenAir,         // daily turns
    UnknownOpenAir,  // hourly turns
    Dungeon          // 10-minute turns
}
