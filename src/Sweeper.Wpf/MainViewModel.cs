using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sweeper.Core.Features.Game;
using Sweeper.Core.Features.Settings;
using Sweeper.Core.Shared;
using Sweeper.Wpf.Features.Board;
using Sweeper.Wpf.Features.Game;

namespace Sweeper.Wpf;

public sealed partial class MainViewModel : ObservableObject
{
  private static readonly TimeSpan SweatingThreshold = TimeSpan.FromSeconds(10);

  private readonly TimeProvider _timeProvider;
  private readonly Random _random;
  private readonly Func<GameSettings, Task<GameSettings?>> _editSettings;
  private readonly DispatcherTimer _timer;
  private readonly List<CellViewModel> _rangeCells = [];

  private GameSettings _settings = Difficulty.Beginner;
  private Game _game;
  private bool _isPressing;
  private CellViewModel? _hoveredCell;

  // editSettings devolve null quando o usuário cancela.
  public MainViewModel(TimeProvider timeProvider, Random random, Func<GameSettings, Task<GameSettings?>> editSettings)
  {
    _timeProvider = timeProvider;
    _random = random;
    _editSettings = editSettings;

    // Menos de 1 s pra o display não pular segundos.
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
    _timer.Tick += (_, _) => OnTimerTick();
    _timer.Start();

    StartGame(_settings);
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

  [RelayCommand]
  private void Reveal(CellViewModel cell) => _game.Reveal(cell.Position);

  [RelayCommand]
  private void ToggleFlag(CellViewModel cell) => _game.ToggleFlag(cell.Position);

  [RelayCommand]
  private void NewGame() => StartGame(_settings);

  // Assíncrono porque o diálogo abre por cima da janela sem bloquear; o comando fica desabilitado até ele fechar.
  [RelayCommand]
  private async Task OpenSettings()
  {
    var settings = await _editSettings(_settings);
    if (settings is null)
    {
      return;
    }

    _settings = settings;
    StartGame(settings);
  }

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
  }

  [RelayCommand]
  private void LeaveCell(CellViewModel cell)
  {
    // MouseLeave da célula antiga pode chegar depois do MouseEnter da nova.
    if (_hoveredCell == cell)
    {
      _hoveredCell = null;
      UpdateRange();
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
      var neighbor = Cells[(position.Y * _game.Board.Width) + position.X];
      if (neighbor.State != CellState.Revealed)
      {
        neighbor.IsInRange = true;
        _rangeCells.Add(neighbor);
      }
    }
  }

  [MemberNotNull(nameof(_game))]
  private void StartGame(GameSettings settings)
  {
    if (_game is not null)
    {
      _game.Changed -= OnGameChanged;
    }

    _hoveredCell = null;
    _rangeCells.Clear();

    _game = new Game(settings, _timeProvider, _random);
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

  private void OnGameChanged(IReadOnlyCollection<GridPosition> positions)
  {
    var isGameOver = _game.IsOver;

    foreach (var position in positions)
    {
      Cells[(position.Y * _game.Board.Width) + position.X].Update(_game.Board[position], isGameOver);
    }

    // O range muda quando vizinhas abrem (chord) e some no fim do jogo.
    UpdateRange();
    RefreshHeader();
  }

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
