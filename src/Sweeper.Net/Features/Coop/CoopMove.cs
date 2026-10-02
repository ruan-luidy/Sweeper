using Sweeper.Core.Shared;

namespace Sweeper.Net.Features.Coop;

public enum CoopMoveKind
{
  Reveal,
  Flag,
}

public readonly record struct CoopMove(CoopMoveKind Kind, GridPosition Position);
