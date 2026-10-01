using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sweeper.Core.Features.Settings;

namespace Sweeper.Wpf.Features.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
  // Preset null = personalizado.
  public sealed record DifficultyOption(string Name, GameSettings? Preset)
  {
    public string Description => Preset is { } preset
      ? $"{preset.Width} × {preset.Height} · {preset.MineCount} minas"
      : "Você escolhe";
  }

  private static readonly DifficultyOption CustomOption = new("Personalizado", null);

  public SettingsViewModel(GameSettings current)
  {
    Options =
    [
      new("Iniciante", Difficulty.Beginner),
      new("Intermediário", Difficulty.Intermediate),
      new("Especialista", Difficulty.Expert),
      CustomOption,
    ];

    BoardWidth = current.Width;
    BoardHeight = current.Height;
    MineCount = current.MineCount;
    HasTimeLimit = current.TimeLimit is not null;
    TimeLimitMinutes = current.TimeLimit is { } limit ? Math.Max(1, (int)limit.TotalMinutes) : 5;

    // O limite de tempo vale pra qualquer dificuldade, então fica fora da comparação.
    var withoutLimit = current with { TimeLimit = null };
    SelectedOption = Options.FirstOrDefault(option => option.Preset == withoutLimit) ?? CustomOption;
  }

  public event Action<GameSettings>? Saved;

  public IReadOnlyList<DifficultyOption> Options { get; }

  public bool IsCustom => SelectedOption.Preset is null;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsCustom))]
  public partial DifficultyOption SelectedOption { get; set; }

  [ObservableProperty]
  public partial int BoardWidth { get; set; }

  [ObservableProperty]
  public partial int BoardHeight { get; set; }

  [ObservableProperty]
  public partial int MineCount { get; set; }

  [ObservableProperty]
  public partial bool HasTimeLimit { get; set; }

  [ObservableProperty]
  public partial int TimeLimitMinutes { get; set; }

  [ObservableProperty]
  public partial string ErrorText { get; private set; } = "";

  partial void OnSelectedOptionChanged(DifficultyOption value)
  {
    if (value.Preset is { } preset)
    {
      BoardWidth = preset.Width;
      BoardHeight = preset.Height;
      MineCount = preset.MineCount;
    }

    Revalidate();
  }

  partial void OnBoardWidthChanged(int value) => Revalidate();

  partial void OnBoardHeightChanged(int value) => Revalidate();

  partial void OnMineCountChanged(int value) => Revalidate();

  partial void OnHasTimeLimitChanged(bool value) => Revalidate();

  partial void OnTimeLimitMinutesChanged(int value) => Revalidate();

  [RelayCommand(CanExecute = nameof(CanSave))]
  private void Save() => Saved?.Invoke(BuildSettings());

  private bool CanSave() => ErrorText.Length == 0;

  private GameSettings BuildSettings() => new(
    BoardWidth,
    BoardHeight,
    MineCount,
    HasTimeLimit ? TimeSpan.FromMinutes(TimeLimitMinutes) : null);

  private void Revalidate()
  {
    // Ainda no construtor; a primeira validação real vem quando SelectedOption é definido.
    if (SelectedOption is null)
    {
      return;
    }

    ErrorText = string.Join(Environment.NewLine, BuildSettings().Validate());
    SaveCommand.NotifyCanExecuteChanged();
  }
}
