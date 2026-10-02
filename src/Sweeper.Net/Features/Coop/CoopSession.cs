using Sweeper.Core.Features.Settings;
using Sweeper.Core.Shared;

namespace Sweeper.Net.Features.Coop;

// Coop em lockstep: as duas máquinas rodam o mesmo Game com a mesma seed e aplicam as mesmas jogadas
// na mesma ordem. Quem decide a ordem é o host; o convidado manda a jogada e só aplica quando o host devolve.
public sealed class CoopSession : IDisposable
{
  private readonly CoopConnection _connection;
  private int _round;

  public CoopSession(CoopConnection connection, bool isHost)
  {
    _connection = connection;
    IsHost = isHost;

    _connection.MessageReceived += OnMessageReceived;
    _connection.Disconnected += OnDisconnected;
    _connection.Start();
  }

  public bool IsHost { get; }

  public string PartnerAddress => _connection.RemoteAddress;

  // Mesmas configurações + mesma seed = mesmo tabuleiro nas duas máquinas.
  public event Action<GameSettings, int>? GameStarted;

  // Jogadas já na ordem do host, prontas pra aplicar no Game.
  public event Action<CoopMove>? MoveApplied;

  // null quando o mouse do parceiro saiu do tabuleiro.
  public event Action<GridPosition?>? PartnerHovered;

  // Só no host: o convidado clicou na carinha.
  public event Action? NewGameRequested;

  public event Action? Disconnected;

  // Só o host começa rodadas. Sem limite de tempo: cada máquina tem seu relógio e o tempo
  // acabaria em momentos diferentes, o que dessincroniza os tabuleiros.
  public void StartGame(GameSettings settings)
  {
    if (!IsHost)
    {
      throw new InvalidOperationException("Só o host começa uma rodada.");
    }

    settings = settings with { TimeLimit = null };
    var seed = Random.Shared.Next();
    _round++;

    _connection.Send(new CoopMessage(
      CoopMessageType.Start,
      _round,
      Seed: seed,
      Width: settings.Width,
      Height: settings.Height,
      MineCount: settings.MineCount));

    GameStarted?.Invoke(settings, seed);
  }

  public void Reveal(GridPosition position) => Play(new CoopMove(CoopMoveKind.Reveal, position));

  public void ToggleFlag(GridPosition position) => Play(new CoopMove(CoopMoveKind.Flag, position));

  public void RequestNewGame()
  {
    if (IsHost)
    {
      NewGameRequested?.Invoke();
    }
    else
    {
      _connection.Send(new CoopMessage(CoopMessageType.NewGame, _round));
    }
  }

  public void Hover(GridPosition? position) =>
    _connection.Send(new CoopMessage(CoopMessageType.Hover, _round, position?.X ?? -1, position?.Y ?? -1));

  public void Dispose()
  {
    _connection.MessageReceived -= OnMessageReceived;
    _connection.Disconnected -= OnDisconnected;
    _connection.Dispose();
  }

  private void Play(CoopMove move)
  {
    _connection.Send(ToMessage(move));

    // O convidado não aplica: espera o host devolver, pra ficar na mesma ordem dele.
    if (IsHost)
    {
      MoveApplied?.Invoke(move);
    }
  }

  private void OnMessageReceived(CoopMessage message)
  {
    switch (message.Type)
    {
      case CoopMessageType.Start when !IsHost:
        _round = message.Round;
        GameStarted?.Invoke(new GameSettings(message.Width, message.Height, message.MineCount), message.Seed);
        break;

      case CoopMessageType.Reveal or CoopMessageType.Flag when IsHost:
        // Jogada de rodada velha: o convidado clicou antes de receber o Start da nova.
        if (message.Round != _round)
        {
          return;
        }

        // Devolve pro convidado na ordem em que o host aplicou.
        _connection.Send(message);
        MoveApplied?.Invoke(ToMove(message));
        break;

      case CoopMessageType.Reveal or CoopMessageType.Flag:
        MoveApplied?.Invoke(ToMove(message));
        break;

      case CoopMessageType.NewGame when IsHost:
        NewGameRequested?.Invoke();
        break;

      case CoopMessageType.Hover:
        PartnerHovered?.Invoke(message.X >= 0 ? new GridPosition(message.X, message.Y) : null);
        break;
    }
  }

  private void OnDisconnected() => Disconnected?.Invoke();

  private CoopMessage ToMessage(CoopMove move)
  {
    var type = move.Kind == CoopMoveKind.Reveal ? CoopMessageType.Reveal : CoopMessageType.Flag;
    return new CoopMessage(type, _round, move.Position.X, move.Position.Y);
  }

  private static CoopMove ToMove(CoopMessage message)
  {
    var kind = message.Type == CoopMessageType.Reveal ? CoopMoveKind.Reveal : CoopMoveKind.Flag;
    return new CoopMove(kind, new GridPosition(message.X, message.Y));
  }
}
