using System.Windows;
using Sweeper.Core.Features.Settings;

namespace Sweeper.Wpf.Features.Settings;

public partial class SettingsWindow : HandyControl.Controls.Window
{
  public SettingsWindow()
  {
    InitializeComponent();
  }

  // Abre modal e devolve as configurações salvas, ou null se cancelou.
  public static GameSettings? Edit(Window owner, GameSettings current)
  {
    var viewModel = new SettingsViewModel(current);
    var window = new SettingsWindow
    {
      Owner = owner,
      DataContext = viewModel,
    };

    GameSettings? result = null;

    viewModel.Saved += settings =>
    {
      result = settings;
      window.DialogResult = true;
    };

    window.ShowDialog();
    return result;
  }
}
