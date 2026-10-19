using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sweeper.Core.Features.Infinite;
using Sweeper.Core.Shared;

namespace Sweeper.Wpf.Features.Infinite;

public sealed partial class InfiniteViewModel : ObservableObject
{
  // Salva no máximo a cada 2 s durante o jogo; fechar o app salva na hora.
  private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(2);

  private readonly TimeProvider _timeProvider;
  private readonly InfiniteStore _store;
  private readonly Func<string, bool> _confirm;
  private readonly Action _openClassic;
  private readonly DispatcherTimer _timer;
  private bool _isDirty;
  private long _lastSave;

  // confirm faz uma pergunta sim/não; openClassic troca a tela pro modo clássico.
  public InfiniteViewModel(TimeProvider timeProvider, InfiniteStore store, Func<string, bool> confirm, Action openClassic)
  {
    _timeProvider = timeProvider;
    _store = store;
    _confirm = confirm;
    _openClassic = openClassic;

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
    _timer.Tick += (_, _) => OnTimerTick();
    _timer.Start();

    Load(store.Preferences.InfiniteMode);
  }

  public Camera Camera { get; } = new();

  [ObservableProperty]
  public partial InfiniteGame Game { get; private set; }

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsEndless), nameof(IsHardcore), nameof(IsTimed))]
  public partial InfiniteMode Mode { get; private set; }

  // Um bool por modo pros RadioButtons. Setter com false é o botão que acabou de ser desmarcado: ignora.
  public bool IsEndless
  {
    get => Mode == InfiniteMode.Endless;
    set => SelectModeIf(value, InfiniteMode.Endless);
  }

  public bool IsHardcore
  {
    get => Mode == InfiniteMode.Hardcore;
    set => SelectModeIf(value, InfiniteMode.Hardcore);
  }

  public bool IsTimed
  {
    get => Mode == InfiniteMode.Timed;
    set => SelectModeIf(value, InfiniteMode.Timed);
  }

  [ObservableProperty]
  public partial int Score { get; private set; }

  [ObservableProperty]
  public partial int BestScore { get; private set; }

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(HasLocked))]
  public partial int LockedCount { get; private set; }

  public bool HasLocked => LockedCount > 0;

  [ObservableProperty]
  public partial bool HasTimeLimit { get; private set; }

  [ObservableProperty]
  public partial string TimeText { get; private set; } = "";

  [ObservableProperty]
  public partial bool IsReady { get; private set; }

  [ObservableProperty]
  public partial bool IsOver { get; private set; }

  [ObservableProperty]
  public partial string OverTitle { get; private set; } = "";

  [ObservableProperty]
  public partial string OverText { get; private set; } = "";

  // Chamado ao fechar o app. O contra o tempo não é salvo, e Hardcore perdido também não (reabre num mundo novo).
  public void Save()
  {
    _isDirty = false;
    _lastSave = _timeProvider.GetTimestamp();

    if (Mode != InfiniteMode.Timed && !Game.IsOver)
    {
      _store.Save(Mode, new InfiniteSave(Game.ToSnapshot(), Camera.CenterX, Camera.CenterY, Camera.CellSize));
    }

    _store.Preferences.InfiniteMode = Mode;
    _store.SavePreferences();
  }

  [RelayCommand]
  private void Reveal(GridPosition position) => Game.Reveal(position);

  [RelayCommand]
  private void ToggleFlag(GridPosition position) => Game.ToggleFlag(position);

  [RelayCommand]
  private void NewWorld()
  {
    if (Game.Status == InfiniteStatus.Playing && !_confirm("Começar um mundo novo? O mapa atual é apagado."))
    {
      return;
    }

    _store.Delete(Mode);
    StartNew(Mode);
  }

  [RelayCommand]
  private void CenterView()
  {
    var start = Game.FirstClick ?? new GridPosition(0, 0);
    Camera.CenterOnCell(start.X, start.Y);
  }

  [RelayCommand]
  private void ZoomIn() => Camera.ZoomBy(1.25);

  [RelayCommand]
  private void ZoomOut() => Camera.ZoomBy(1 / 1.25);

  [RelayCommand]
  private void OpenClassic()
  {
    Save();
    _openClassic();
  }

  private void SelectModeIf(bool selected, InfiniteMode mode)
  {
    if (!selected || mode == Mode)
    {
      return;
    }

    if (Mode == InfiniteMode.Timed && Game.Status == InfiniteStatus.Playing &&
        !_confirm("Sair do contra o tempo? A partida atual é perdida."))
    {
      // Devolve a seleção dos RadioButtons pro modo atual.
      OnPropertyChanged(nameof(IsEndless));
      OnPropertyChanged(nameof(IsHardcore));
      OnPropertyChanged(nameof(IsTimed));
      return;
    }

    Save();
    Load(mode);
  }

  [MemberNotNull(nameof(Game))]
  private void Load(InfiniteMode mode)
  {
    if (mode != InfiniteMode.Timed && _store.Load(mode) is { } saved && TryRestore(saved.Game) is { } game)
    {
      SetGame(game);
      Camera.CenterX = saved.CenterX;
      Camera.CenterY = saved.CenterY;
      Camera.CellSize = Math.Clamp(saved.CellSize, Camera.MinCellSize, Camera.MaxCellSize);
      return;
    }

    StartNew(mode);
  }

  // Save de outra versão ou mexido na mão: começa do zero em vez de travar.
  private InfiniteGame? TryRestore(InfiniteSnapshot snapshot)
  {
    try
    {
      return InfiniteGame.Restore(snapshot, _timeProvider);
    }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException)
    {
      return null;
    }
  }

  [MemberNotNull(nameof(Game))]
  private void StartNew(InfiniteMode mode)
  {
    SetGame(new InfiniteGame(InfiniteSettings.For(mode), Random.Shared.Next(), _timeProvider));
    Camera.Reset();
  }

  [MemberNotNull(nameof(Game))]
  private void SetGame(InfiniteGame game)
  {
    if (Game is not null)
    {
      Game.Changed -= OnGameChanged;
    }

    Game = game;
    Mode = game.Settings.Mode;
    HasTimeLimit = game.Settings.TimeLimit is not null;
    BestScore = _store.Preferences.GetBest(Mode);
    game.Changed += OnGameChanged;

    _isDirty = false;
    Refresh();
  }

  private void OnGameChanged()
  {
    _isDirty = true;
    Refresh();

    if (Game.IsOver)
    {
      // Hardcore perdido não volta: apaga o save pra reabrir num mundo novo.
      _store.Delete(Mode);
      _store.SavePreferences();
    }
  }

  private void Refresh()
  {
    Score = Game.SolvedCount;
    LockedCount = Game.LockedCount;
    IsReady = Game.Status == InfiniteStatus.Ready;
    IsOver = Game.IsOver;

    if (Score > BestScore)
    {
      BestScore = Score;
      _store.Preferences.SetBest(Mode, Score);
    }

    OverTitle = Game.Status == InfiniteStatus.TimeUp ? "Tempo esgotado!" : "Explodiu!";
    OverText = $"Você resolveu {Score} {(Score == 1 ? "setor" : "setores")}. Recorde: {BestScore}.";

    UpdateTime();
  }

  private void UpdateTime()
  {
    if (Game.TimeRemaining is not { } remaining)
    {
      TimeText = "";
      return;
    }

    // Arredonda pra cima: 00:00 só quando acabou mesmo.
    var time = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
    TimeText = $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
  }

  private void OnTimerTick()
  {
    Game.Tick();
    UpdateTime();

    if (_isDirty && _timeProvider.GetElapsedTime(_lastSave) >= SaveInterval)
    {
      Save();
    }
  }
}
