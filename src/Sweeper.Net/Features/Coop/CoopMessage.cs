namespace Sweeper.Net.Features.Coop;

public enum CoopMessageType
{
  // Host -> convidado: rodada nova com configurações e seed.
  Start,

  Reveal,
  Flag,

  // Convidado -> host: pede pra começar de novo (clicou na carinha).
  NewGame,

  // Célula sob o mouse do parceiro. X = -1 quando o mouse saiu do tabuleiro.
  Hover,
}

// Uma linha de JSON por mensagem. Os campos que o tipo não usa ficam no padrão.
public sealed record CoopMessage(
  CoopMessageType Type,
  int Round = 0,
  int X = -1,
  int Y = -1,
  int Seed = 0,
  int Width = 0,
  int Height = 0,
  int MineCount = 0);
