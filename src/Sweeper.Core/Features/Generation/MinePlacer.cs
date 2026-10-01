using Sweeper.Core.Shared;

namespace Sweeper.Core.Features.Generation;

public static class MinePlacer
{
  // Roda no primeiro clique pra deixar a célula clicada e as vizinhas fora do sorteio.
  public static void Place(Board board, GridPosition safeStart, Random random)
  {
    if (board.IsMinesPlaced)
    {
      throw new InvalidOperationException("As minas deste tabuleiro já foram sorteadas.");
    }

    var safeArea = new HashSet<GridPosition>(board.Neighbors(safeStart)) { safeStart };
    var candidates = board.AllPositions().Where(position => !safeArea.Contains(position)).ToList();

    if (board.MineCount > candidates.Count)
    {
      throw new InvalidOperationException("Não há células suficientes fora da área segura para todas as minas.");
    }

    // Fisher-Yates parcial: sorteio sem repetição.
    for (var i = 0; i < board.MineCount; i++)
    {
      var j = random.Next(i, candidates.Count);
      (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
      board[candidates[i]].IsMine = true;
    }

    foreach (var position in board.AllPositions())
    {
      board[position].AdjacentMines = board.Neighbors(position).Count(neighbor => board[neighbor].IsMine);
    }

    board.IsMinesPlaced = true;
  }
}
