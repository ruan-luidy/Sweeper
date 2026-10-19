using Sweeper.Core.Shared;

namespace Sweeper.Core.Features.Infinite;

// Só o que o jogador mudou: as minas saem de novo da seed + primeiro clique.
public sealed record InfiniteSnapshot(
  InfiniteMode Mode,
  int MinesPerSector,
  int Seed,
  GridPosition? FirstClick,
  InfiniteStatus Status,
  IReadOnlyList<SectorSnapshot> Sectors);

public sealed record SectorSnapshot(int X, int Y, SectorState State, ulong Revealed, ulong Flagged);
