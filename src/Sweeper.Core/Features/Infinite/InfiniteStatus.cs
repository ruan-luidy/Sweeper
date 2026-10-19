namespace Sweeper.Core.Features.Infinite;

public enum InfiniteStatus
{
  Ready,
  Playing,

  // Só no Hardcore.
  Lost,

  // Só no Timed.
  TimeUp,
}
