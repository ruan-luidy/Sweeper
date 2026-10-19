using System.Numerics;

namespace Sweeper.Core.Features.Infinite;

// 8x8 células guardadas em bits. Bit da célula = y * 8 + x, em coordenadas locais do setor.
public sealed class Sector
{
  public const int Size = 8;
  public const int CellCount = Size * Size;

  // Minas vizinhas de cada célula aberta; -1 = ainda não calculada.
  private readonly sbyte[] _adjacent = new sbyte[CellCount];

  internal Sector(SectorPosition position, ulong mines)
  {
    Position = position;
    Mines = mines;
    Array.Fill(_adjacent, (sbyte)-1);
  }

  public SectorPosition Position { get; }

  public SectorState State { get; internal set; } = SectorState.Open;

  public int MineCount => BitOperations.PopCount(Mines);

  // Células sem mina já abertas.
  public int RevealedSafeCount => BitOperations.PopCount(Revealed & ~Mines);

  public int SafeCount => CellCount - MineCount;

  internal ulong Mines { get; }

  internal ulong Revealed { get; set; }

  internal ulong Flagged { get; set; }

  internal bool AllSafeRevealed => (Revealed | Mines) == ulong.MaxValue;

  internal static ulong Bit(int index) => 1UL << index;

  internal int GetAdjacent(int index) => _adjacent[index];

  internal void SetAdjacent(int index, int count) => _adjacent[index] = (sbyte)count;
}
