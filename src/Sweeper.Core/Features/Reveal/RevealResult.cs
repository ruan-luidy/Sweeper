using Sweeper.Core.Shared;

namespace Sweeper.Core.Features.Reveal;

public sealed record RevealResult(IReadOnlyList<GridPosition> Revealed, bool HitMine)
{
  public static RevealResult Empty { get; } = new([], false);

  public RevealResult Combine(RevealResult other) =>
    new([.. Revealed, .. other.Revealed], HitMine || other.HitMine);
}
