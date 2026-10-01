namespace Sweeper.Core.Shared;

// Setters internal: só o Core altera, A UI só lê.
public sealed class Cell
{
  public bool IsMine { get; internal set; }

  public int AdjacentMines { get; internal set; }

  public CellState State { get; internal set; } = CellState.Hidden;
}
