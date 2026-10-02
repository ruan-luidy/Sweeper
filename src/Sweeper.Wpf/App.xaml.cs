using System.Windows;
using HandyControl.Controls;
using Sweeper.Wpf.Features.Coop;
using Sweeper.Wpf.Features.Settings;

namespace Sweeper.Wpf;

public partial class App : Application
{
  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    // O ViewModel recebe os diálogos e o aviso como funções, sem conhecer as telas.
    var window = new MainWindow();
    window.DataContext = new MainViewModel(
      TimeProvider.System,
      Random.Shared,
      SettingsDialog.EditAsync,
      CoopDialog.ConnectAsync,
      message => Growl.InfoGlobal(message));

    window.Show();
  }
}
