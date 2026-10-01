namespace Sweeper.Core.Features.Settings;

public static class Difficulty
{
  public static GameSettings Beginner { get; } = new(9, 9, 10);

  public static GameSettings Intermediate { get; } = new(16, 16, 40);

  public static GameSettings Expert { get; } = new(30, 16, 99);
}
