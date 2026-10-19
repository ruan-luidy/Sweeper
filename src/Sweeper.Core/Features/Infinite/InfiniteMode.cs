namespace Sweeper.Core.Features.Infinite;

public enum InfiniteMode
{
  // Mina trava o setor e o jogo segue.
  Endless,

  // Uma mina e acabou.
  Hardcore,

  // Mina trava o setor; vale quantos setores resolver antes do tempo acabar.
  Timed,
}
