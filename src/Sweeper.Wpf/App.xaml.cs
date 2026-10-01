using System.Windows;
using Sweeper.Wpf.Features.Settings;

namespace Sweeper.Wpf;

public partial class App : Application
{
  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    // O ViewModel recebe a tela de configurações como função, sem conhecer o diálogo.
    var window = new MainWindow();
    window.DataContext = new MainViewModel(
      TimeProvider.System,
      Random.Shared,
      SettingsDialog.EditAsync);

    window.Show();
  }
}
