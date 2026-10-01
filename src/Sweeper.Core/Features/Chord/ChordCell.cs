using Sweeper.Core.Features.Reveal;
using Sweeper.Core.Shared;

namespace Sweeper.Core.Features.Chord;

// Número aberto com a quantidade certa de bandeiras em volta: abre todas as vizinhas fechadas.
public static class ChordCell
{
  public static RevealResult Execute(Board board, GridPosition position)
  {
    var cell = board[position];

    if (cell.State != CellState.Revealed || cell.IsMine || cell.AdjacentMines == 0)
    {
      return RevealResult.Empty;
    }

    var neighbors = board.Neighbors(position).ToList();
    var flagCount = neighbors.Count(neighbor => board[neighbor].State == CellState.Flagged);

    if (flagCount != cell.AdjacentMines)
    {
      return RevealResult.Empty;
    }

    // RevealCell já ignora bandeiras e células abertas.
    var result = RevealResult.Empty;
    foreach (var neighbor in neighbors)
    {
      result = result.Combine(RevealCell.Execute(board, neighbor));
    }

    return result;
  }
}
