using System.Windows.Controls;
using HandyControl.Controls;
using Sweeper.Core.Features.Settings;

namespace Sweeper.Wpf.Features.Settings;

public partial class SettingsDialog : UserControl
{
  public SettingsDialog()
  {
    InitializeComponent();
  }

  // Abre por cima da janela ativa (hc:Dialog) e devolve as configurações salvas, ou null se cancelou.
  public static Task<GameSettings?> EditAsync(GameSettings current)
  {
    var viewModel = new SettingsViewModel(current);
    var dialog = Dialog.Show(new SettingsDialog { DataContext = viewModel });
    var result = new TaskCompletionSource<GameSettings?>();

    viewModel.Saved += settings =>
    {
      dialog.Close();
      result.TrySetResult(settings);
    };

    viewModel.Cancelled += () =>
    {
      dialog.Close();
      result.TrySetResult(null);
    };

    return result.Task;
  }
}
