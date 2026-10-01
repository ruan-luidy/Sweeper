using Sweeper.Core.Features.Chord;
using Sweeper.Core.Features.Generation;
using Sweeper.Core.Features.Reveal;
using Sweeper.Core.Features.Settings;
using Sweeper.Core.Shared;

// Alias porque o método ToggleFlag do Game esconderia a classe.
using ToggleFlagFeature = Sweeper.Core.Features.Flag.ToggleFlag;

namespace Sweeper.Core.Features.Game;

public sealed class Game
{
  private readonly TimeProvider _timeProvider;
  private readonly Random _random;
  private readonly int _safeCellCount;
  private int _revealedSafeCells;
  private int _flagsPlaced;
  private long _startTimestamp;
  private TimeSpan _finalElapsed;

  public Game(GameSettings settings, TimeProvider timeProvider, Random random)
  {
    var errors = settings.Validate();
    if (errors.Count > 0)
    {
      throw new ArgumentException(string.Join(" ", errors), nameof(settings));
    }

    Settings = settings;
    _timeProvider = timeProvider;
    _random = random;

    Board = new Board(settings.Width, settings.Height, settings.MineCount);
    _safeCellCount = (settings.Width * settings.Height) - settings.MineCount;
  }

  // Posições que mudaram, pra UI atualizar só elas.
  public event Action<IReadOnlyCollection<GridPosition>>? Changed;

  public GameSettings Settings { get; }

  public Board Board { get; }

  public GameStatus Status { get; private set; } = GameStatus.Ready;

  public bool IsOver => Status is GameStatus.Won or GameStatus.Lost;

  // Pode ficar negativo, igual ao original.
  public int FlagsRemaining => Board.MineCount - _flagsPlaced;

  public TimeSpan Elapsed => Status switch
  {
    GameStatus.Ready => TimeSpan.Zero,
    GameStatus.Playing => _timeProvider.GetElapsedTime(_startTimestamp),
    _ => _finalElapsed,
  };

  public TimeSpan? TimeRemaining => Settings.TimeLimit is { } limit
    ? (limit > Elapsed ? limit - Elapsed : TimeSpan.Zero)
    : null;

  public void Reveal(GridPosition position)
  {
    // O tempo pode ter acabado entre o último Tick e este clique.
    CheckTimeLimit();

    if (IsOver)
    {
      return;
    }

    if (Status == GameStatus.Ready)
    {
      // Clique em bandeira não inicia o jogo.
      if (Board[position].State == CellState.Flagged)
      {
        return;
      }

      MinePlacer.Place(Board, position, _random);
      _startTimestamp = _timeProvider.GetTimestamp();
      Status = GameStatus.Playing;
    }

    var result = Board[position].State == CellState.Revealed
      ? ChordCell.Execute(Board, position)
      : RevealCell.Execute(Board, position);

    Apply(result);
  }

  public void ToggleFlag(GridPosition position)
  {
    if (IsOver)
    {
      return;
    }

    if (!ToggleFlagFeature.Execute(Board, position))
    {
      return;
    }

    _flagsPlaced += Board[position].State == CellState.Flagged ? 1 : -1;
    Changed?.Invoke([position]);
  }

  // Chamado pelo timer da UI; o Core não tem timer próprio.
  public void Tick() => CheckTimeLimit();

  private void CheckTimeLimit()
  {
    if (Status != GameStatus.Playing || Settings.TimeLimit is not { } limit)
    {
      return;
    }

    if (Elapsed < limit)
    {
      return;
    }

    Finish(GameStatus.Lost);
    Changed?.Invoke(LossPositions().ToList());
  }

  private void Apply(RevealResult result)
  {
    if (result.Revealed.Count == 0)
    {
      return;
    }

    // HashSet porque a mina explodida aparece em Revealed e em MinePositions.
    var affected = new HashSet<GridPosition>(result.Revealed);

    if (result.HitMine)
    {
      Finish(GameStatus.Lost);
      affected.UnionWith(LossPositions());
    }
    else
    {
      _revealedSafeCells += result.Revealed.Count;

      if (_revealedSafeCells == _safeCellCount)
      {
        Finish(GameStatus.Won);

        foreach (var mine in MinePositions())
        {
          if (Board[mine].State == CellState.Hidden)
          {
            Board[mine].State = CellState.Flagged;
            affected.Add(mine);
          }
        }

        _flagsPlaced = Board.MineCount;
      }
    }

    Changed?.Invoke(affected);
  }

  // Congela o tempo antes de trocar o status, porque Elapsed depende dele.
  private void Finish(GameStatus status)
  {
    _finalElapsed = _timeProvider.GetElapsedTime(_startTimestamp);
    Status = status;
  }

  private IEnumerable<GridPosition> MinePositions() =>
    Board.AllPositions().Where(position => Board[position].IsMine);

  // Na derrota as bandeiras também mudam de cara (certa ou errada), então entram no Changed.
  private IEnumerable<GridPosition> LossPositions() =>
    Board.AllPositions().Where(position => Board[position].IsMine || Board[position].State == CellState.Flagged);
}
