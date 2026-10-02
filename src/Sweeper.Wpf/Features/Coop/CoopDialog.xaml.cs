using System.Windows.Controls;
using HandyControl.Controls;
using Sweeper.Net.Features.Coop;

namespace Sweeper.Wpf.Features.Coop;

public partial class CoopDialog : UserControl
{
  public CoopDialog()
  {
    InitializeComponent();
  }

  // Abre por cima da janela ativa (hc:Dialog) e devolve a sessão conectada, ou null se cancelou.
  public static Task<CoopSession?> ConnectAsync()
  {
    var viewModel = new CoopViewModel();
    var dialog = Dialog.Show(new CoopDialog { DataContext = viewModel });
    var result = new TaskCompletionSource<CoopSession?>();

    viewModel.Connected += session =>
    {
      dialog.Close();
      result.TrySetResult(session);
    };

    viewModel.Cancelled += () =>
    {
      dialog.Close();
      result.TrySetResult(null);
    };

    return result.Task;
  }
}
