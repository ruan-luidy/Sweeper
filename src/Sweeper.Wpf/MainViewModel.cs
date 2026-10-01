using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sweeper.Core.Features.Game;
using Sweeper.Core.Features.Settings;
using Sweeper.Core.Shared;
using Sweeper.Wpf.Features.Board;

namespace Sweeper.Wpf;

public sealed partial class MainViewModel : ObservableObject
{
  private readonly TimeProvider _timeProvider;
  private readonly Random _random;
  private readonly Func<GameSettings, GameSettings?> _editSettings;
  private readonly DispatcherTimer _timer;

  private GameSettings _settings = Difficulty.Beginner;
  private Game _game;

  // editSettings devolve null quando o usuário cancela.
  public MainViewModel(TimeProvider timeProvider, Random random, Func<GameSettings, GameSettings?> editSettings)
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

  [RelayCommand]
  private void Reveal(CellViewModel cell) => _game.Reveal(cell.Position);

  [RelayCommand]
  private void ToggleFlag(CellViewModel cell) => _game.ToggleFlag(cell.Position);

  [RelayCommand]
  private void NewGame() => StartGame(_settings);

  [RelayCommand]
  private void OpenSettings()
  {
    var settings = _editSettings(_settings);
    if (settings is null)
    {
      return;
    }

    _settings = settings;
    StartGame(settings);
  }

  [MemberNotNull(nameof(_game))]
  private void StartGame(GameSettings settings)
  {
    if (_game is not null)
    {
      _game.Changed -= OnGameChanged;
    }

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
  }
}
