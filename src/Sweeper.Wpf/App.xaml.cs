using System.Windows;
using HandyControl.Controls;
using Sweeper.Wpf.Features.Coop;
using Sweeper.Wpf.Features.Infinite;
using Sweeper.Wpf.Features.Settings;
using MessageBox = HandyControl.Controls.MessageBox;

namespace Sweeper.Wpf;

public partial class App : Application
{
  private ShellViewModel? _shell;

  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    // Os ViewModels recebem os diálogos e avisos como funções, sem conhecer as telas.
    var store = new InfiniteStore();
    _shell = new ShellViewModel(
      openInfinite => new MainViewModel(
        TimeProvider.System,
        Random.Shared,
        SettingsDialog.EditAsync,
        CoopDialog.ConnectAsync,
        message => Growl.InfoGlobal(message),
        openInfinite),
      openClassic => new InfiniteViewModel(
        TimeProvider.System,
        store,
        question => MessageBox.Show(question, "Sweeper", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes,
        openClassic));

    var window = new MainWindow { DataContext = _shell };
    window.Show();
  }

  protected override void OnExit(ExitEventArgs e)
  {
    _shell?.Save();
    base.OnExit(e);
  }
}
