using Sweeper.Core.Shared;

namespace Sweeper.Core.Features.Reveal;

public static class RevealCell
{
  public static RevealResult Execute(Board board, GridPosition position)
  {
    var cell = board[position];

    if (cell.State != CellState.Hidden)
    {
      return RevealResult.Empty;
    }

    cell.State = CellState.Revealed;

    // A mina fica Revealed: é assim que a UI sabe qual explodiu.
    if (cell.IsMine)
    {
      return new RevealResult([position], HitMine: true);
    }

    var revealed = new List<GridPosition> { position };

    if (cell.AdjacentMines > 0)
    {
      return new RevealResult(revealed, HitMine: false);
    }

    // BFS com fila em vez de recursão pra não estourar a pilha em tabuleiro grande.
    var queue = new Queue<GridPosition>();
    queue.Enqueue(position);

    while (queue.TryDequeue(out var current))
    {
      foreach (var neighborPosition in board.Neighbors(current))
      {
        var neighbor = board[neighborPosition];

        if (neighbor.State != CellState.Hidden)
        {
          continue;
        }

        // Vizinha de 0 nunca é mina.
        neighbor.State = CellState.Revealed;
        revealed.Add(neighborPosition);

        if (neighbor.AdjacentMines == 0)
        {
          queue.Enqueue(neighborPosition);
        }
      }
    }

    return new RevealResult(revealed, HitMine: false);
  }
}
