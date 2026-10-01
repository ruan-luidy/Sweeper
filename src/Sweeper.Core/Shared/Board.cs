namespace Sweeper.Core.Shared;

// Só a grade e a geometria. As regras do jogo ficam nas features.
public sealed class Board
{
  private readonly Cell[,] _cells;

  public Board(int width, int height, int mineCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
    ArgumentOutOfRangeException.ThrowIfNegative(mineCount);

    Width = width;
    Height = height;
    MineCount = mineCount;

    _cells = new Cell[width, height];
    foreach (var position in AllPositions())
    {
      _cells[position.X, position.Y] = new Cell();
    }
  }

  public int Width { get; }

  public int Height { get; }

  public int MineCount { get; }

  // As minas só são sorteadas no primeiro clique.
  public bool IsMinesPlaced { get; internal set; }

  public Cell this[GridPosition position]
  {
    get
    {
      if (!Contains(position))
      {
        throw new ArgumentOutOfRangeException(nameof(position), position, "Posição fora do tabuleiro.");
      }

      return _cells[position.X, position.Y];
    }
  }

  public bool Contains(GridPosition position) =>
    position.X >= 0 && position.X < Width &&
    position.Y >= 0 && position.Y < Height;

  public IEnumerable<GridPosition> Neighbors(GridPosition position)
  {
    for (var dy = -1; dy <= 1; dy++)
    {
      for (var dx = -1; dx <= 1; dx++)
      {
        if (dx == 0 && dy == 0)
        {
          continue;
        }

        var neighbor = new GridPosition(position.X + dx, position.Y + dy);
        if (Contains(neighbor))
        {
          yield return neighbor;
        }
      }
    }
  }

  // Linha por linha, a mesma ordem do UniformGrid: índice na UI = y * Width + x.
  public IEnumerable<GridPosition> AllPositions()
  {
    for (var y = 0; y < Height; y++)
    {
      for (var x = 0; x < Width; x++)
      {
        yield return new GridPosition(x, y);
      }
    }
  }
}
