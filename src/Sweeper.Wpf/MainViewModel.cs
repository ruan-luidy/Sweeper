using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sweeper.Core.Features.Game;
using Sweeper.Core.Features.Settings;
using Sweeper.Core.Shared;
using Sweeper.Net.Features.Coop;
using Sweeper.Wpf.Features.Board;
using Sweeper.Wpf.Features.Game;

namespace Sweeper.Wpf;

public sealed partial class MainViewModel : ObservableObject
{
  private static readonly TimeSpan SweatingThreshold = TimeSpan.FromSeconds(10);

  private readonly TimeProvider _timeProvider;
  private readonly Random _random;
  private readonly Func<GameSettings, Task<GameSettings?>> _editSettings;
  private readonly Func<Task<CoopSession?>> _connectCoop;
  private readonly Action<string> _notify;
  private readonly DispatcherTimer _timer;
  private readonly List<CellViewModel> _rangeCells = [];

  private GameSettings _settings = Difficulty.Beginner;
  private Game _game;
  private bool _isPressing;
  private CellViewModel? _hoveredCell;
  private CellViewModel? _partnerCell;

  // null = jogando sozinho.
  private CoopSession? _coop;

  // editSettings e connectCoop devolvem null quando o usuário cancela. notify mostra um aviso rápido.
  public MainViewModel(
    TimeProvider timeProvider,
    Random random,
    Func<GameSettings, Task<GameSettings?>> editSettings,
    Func<Task<CoopSession?>> connectCoop,
    Action<string> notify)
  {
    _timeProvider = timeProvider;
    _random = random;
    _editSettings = editSettings;
    _connectCoop = connectCoop;
    _notify = notify;

    // Menos de 1 s pra o display não pular segundos.
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
    _timer.Tick += (_, _) => OnTimerTick();
    _timer.Start();

    StartGame(_settings, _random);
  }

  // Array trocado inteiro a cada jogo novo; durante a partida só as células mudam por dentro.
  [ObservableProperty]
  public partial IReadOnlyList<CellViewModel> Cells { get; private set; } = [];

  [ObservableProperty]
  public partial int Columns { get; private set; }

  [ObservableProperty]
  public partial int MinesRemaining { get; private set; }

  [ObservableProperty]
  public partial string TimeText { get; private set; } = "00:00";

  [ObservableProperty]
  public partial bool HasTimeLimit { get; private set; }

  [ObservableProperty]
  public partial GameStatus Status { get; private set; }

  [ObservableProperty]
  public partial GameFace Face { get; private set; }

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(OpenSettingsCommand))]
  public partial bool IsCoop { get; private set; }

  [ObservableProperty]
  public partial string CoopText { get; private set; } = "";

  // No coop a jogada passa pela sessão, que devolve na ordem do host (OnCoopMoveApplied).
  [RelayCommand]
  private void Reveal(CellViewModel cell)
  {
    if (_coop is null)
    {
      _game.Reveal(cell.Position);
    }
    else
    {
      _coop.Reveal(cell.Position);
    }
  }

  [RelayCommand]
  private void ToggleFlag(CellViewModel cell)
  {
    if (_coop is null)
    {
      _game.ToggleFlag(cell.Position);
    }
    else
    {
      _coop.ToggleFlag(cell.Position);
    }
  }

  // No coop o convidado só pede; quem começa a rodada é o host.
  [RelayCommand]
  private void NewGame()
  {
    if (_coop is { IsHost: false })
    {
      _coop.RequestNewGame();
      return;
    }

    StartRound(_settings);
  }

  // Assíncrono porque o diálogo abre por cima da janela sem bloquear; o comando fica desabilitado até ele fechar.
  [RelayCommand(CanExecute = nameof(CanOpenSettings))]
  private async Task OpenSettings()
  {
    var settings = await _editSettings(_settings);
    if (settings is null)
    {
      return;
    }

    _settings = settings;
    StartRound(settings);
  }

  // O convidado joga com as configurações do host.
  private bool CanOpenSettings() => _coop is not { IsHost: false };

  [RelayCommand]
  private async Task OpenCoop()
  {
    var session = await _connectCoop();
    if (session is not null)
    {
      BeginCoop(session);
    }
  }

  [RelayCommand]
  private void LeaveCoop() => EndCoop(message: null);

  [RelayCommand]
  private void Press()
  {
    _isPressing = true;
    UpdateFace();
  }

  [RelayCommand]
  private void Release()
  {
    _isPressing = false;
    UpdateFace();
  }

  [RelayCommand]
  private void HoverCell(CellViewModel cell)
  {
    _hoveredCell = cell;
    UpdateRange();
    _coop?.Hover(cell.Position);
  }

  [RelayCommand]
  private void LeaveCell(CellViewModel cell)
  {
    // MouseLeave da célula antiga pode chegar depois do MouseEnter da nova.
    if (_hoveredCell == cell)
    {
      _hoveredCell = null;
      UpdateRange();
      _coop?.Hover(null);
    }
  }

  // Liga o range nas vizinhas fechadas do número sob o mouse e desliga o anterior.
  private void UpdateRange()
  {
    foreach (var cell in _rangeCells)
    {
      cell.IsInRange = false;
      cell.IsRangeCenter = false;
    }

    _rangeCells.Clear();

    if (_hoveredCell is not { State: CellState.Revealed, AdjacentMines: > 0 } center || _game.IsOver)
    {
      return;
    }

    center.IsRangeCenter = true;
    _rangeCells.Add(center);

    foreach (var position in _game.Board.Neighbors(center.Position))
    {
      var neighbor = CellAt(position);
      if (neighbor.State != CellState.Revealed)
      {
        neighbor.IsInRange = true;
        _rangeCells.Add(neighbor);
      }
    }
  }

  // Sozinho começa direto; no coop o host sorteia a seed e a sessão chama OnCoopGameStarted nas duas máquinas.
  private void StartRound(GameSettings settings)
  {
    if (_coop is { IsHost: true })
    {
      _coop.StartGame(settings);
    }
    else
    {
      StartGame(settings, _random);
    }
  }

  [MemberNotNull(nameof(_game))]
  private void StartGame(GameSettings settings, Random random)
  {
    if (_game is not null)
    {
      _game.Changed -= OnGameChanged;
    }

    _hoveredCell = null;
    _partnerCell = null;
    _rangeCells.Clear();

    _game = new Game(settings, _timeProvider, random);
    _game.Changed += OnGameChanged;

    var cells = _game.Board.AllPositions().Select(position => new CellViewModel(position)).ToArray();
    foreach (var cell in cells)
    {
      cell.Update(_game.Board[cell.Position], isGameOver: false);
    }

    // Columns antes de Cells pro UniformGrid já ter o número certo.
    Columns = _game.Board.Width;
    Cells = cells;
    HasTimeLimit = settings.TimeLimit is not null;

    RefreshHeader();
  }

  private void BeginCoop(CoopSession session)
  {
    _coop = session;
    session.GameStarted += OnCoopGameStarted;
    session.MoveApplied += OnCoopMoveApplied;
    session.PartnerHovered += OnPartnerHovered;
    session.NewGameRequested += OnCoopNewGameRequested;
    session.Disconnected += OnCoopDisconnected;

    IsCoop = true;
    CoopText = session.IsHost
      ? $"Coop: você é o host, parceiro em {session.PartnerAddress}"
      : $"Coop: conectado ao host {session.PartnerAddress}";

    // O convidado continua no tabuleiro dele até o Start do host chegar.
    if (session.IsHost)
    {
      session.StartGame(_settings);
    }
  }

  // message null = saiu por vontade própria, sem aviso.
  private void EndCoop(string? message)
  {
    if (_coop is not { } session)
    {
      return;
    }

    session.GameStarted -= OnCoopGameStarted;
    session.MoveApplied -= OnCoopMoveApplied;
    session.PartnerHovered -= OnPartnerHovered;
    session.NewGameRequested -= OnCoopNewGameRequested;
    session.Disconnected -= OnCoopDisconnected;
    session.Dispose();

    _coop = null;
    IsCoop = false;
    CoopText = "";

    StartGame(_settings, _random);

    if (message is not null)
    {
      _notify(message);
    }
  }

  // Mesma seed nas duas máquinas: Random novo com ela, não o compartilhado.
  private void OnCoopGameStarted(GameSettings settings, int seed) => StartGame(settings, new Random(seed));

  private void OnCoopMoveApplied(CoopMove move)
  {
    if (move.Kind == CoopMoveKind.Reveal)
    {
      _game.Reveal(move.Position);
    }
    else
    {
      _game.ToggleFlag(move.Position);
    }
  }

  private void OnCoopNewGameRequested() => StartRound(_settings);

  private void OnCoopDisconnected() => EndCoop("O parceiro saiu do coop. Voltando pro jogo solo.");

  private void OnPartnerHovered(GridPosition? position)
  {
    if (_partnerCell is not null)
    {
      _partnerCell.IsPartnerHover = false;
      _partnerCell = null;
    }

    // Pode chegar um hover da rodada anterior, com tabuleiro de outro tamanho.
    if (position is { } target && _game.Board.Contains(target))
    {
      _partnerCell = CellAt(target);
      _partnerCell.IsPartnerHover = true;
    }
  }

  private void OnGameChanged(IReadOnlyCollection<GridPosition> positions)
  {
    var isGameOver = _game.IsOver;

    foreach (var position in positions)
    {
      CellAt(position).Update(_game.Board[position], isGameOver);
    }

    // O range muda quando vizinhas abrem (chord) e some no fim do jogo.
    UpdateRange();
    RefreshHeader();
  }

  private CellViewModel CellAt(GridPosition position) => Cells[(position.Y * _game.Board.Width) + position.X];

  private void OnTimerTick()
  {
    _game.Tick();
    UpdateTime();
  }

  private void RefreshHeader()
  {
    MinesRemaining = _game.FlagsRemaining;
    Status = _game.Status;
    UpdateTime();
  }

  private void UpdateTime()
  {
    // Contagem regressiva arredonda pra cima: 00:00 só quando acabou mesmo.
    var time = _game.TimeRemaining is { } remaining
      ? TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds))
      : _game.Elapsed;

    // TotalMinutes porque "mm" volta pra 00 depois de uma hora.
    TimeText = $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    UpdateFace();
  }

  // Fim de jogo ganha de tudo; depois o mouse segurado; depois o tempo acabando.
  private void UpdateFace()
  {
    Face = _game.Status switch
    {
      GameStatus.Won => GameFace.Won,
      GameStatus.Lost => GameFace.Lost,
      _ when _isPressing => GameFace.Nervous,
      GameStatus.Playing when _game.TimeRemaining <= SweatingThreshold => GameFace.Sweating,
      _ => GameFace.Happy,
    };
  }
}
