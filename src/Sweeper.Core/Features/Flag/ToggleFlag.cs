using Sweeper.Core.Shared;

namespace Sweeper.Core.Features.Flag;

public static class ToggleFlag
{
  // true se a célula mudou.
  public static bool Execute(Board board, GridPosition position)
  {
    var cell = board[position];

    switch (cell.State)
    {
      case CellState.Hidden:
        cell.State = CellState.Flagged;
        return true;

      case CellState.Flagged:
        cell.State = CellState.Hidden;
        return true;

      default:
        return false;
    }
  }
}
