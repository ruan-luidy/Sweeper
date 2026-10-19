using System.ComponentModel;
using System.Windows;

namespace Sweeper.Wpf;

public partial class MainWindow : HandyControl.Controls.Window
{
  private const double InfiniteWidth = 1200;
  private const double InfiniteHeight = 800;

  public MainWindow()
  {
    InitializeComponent();
    DataContextChanged += OnDataContextChanged;
  }

  private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
  {
    if (e.OldValue is ShellViewModel oldShell)
    {
      oldShell.PropertyChanged -= OnShellPropertyChanged;
    }

    if (e.NewValue is ShellViewModel shell)
    {
      shell.PropertyChanged += OnShellPropertyChanged;
      ApplyLayout(shell.IsInfinite);
    }
  }

  private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(ShellViewModel.IsInfinite) && sender is ShellViewModel shell)
    {
      ApplyLayout(shell.IsInfinite);
    }
  }

  // Clássico: a janela segue o tabuleiro e não redimensiona (mexer na mão desligaria o SizeToContent nos jogos seguintes).
  // Infinito: janela grande e redimensionável, o mapa ocupa o que tiver.
  private void ApplyLayout(bool isInfinite)
  {
    if (!isInfinite)
    {
      WindowState = WindowState.Normal;
      ResizeMode = ResizeMode.CanMinimize;
      SizeToContent = SizeToContent.WidthAndHeight;
      return;
    }

    var area = SystemParameters.WorkArea;
    SizeToContent = SizeToContent.Manual;
    ResizeMode = ResizeMode.CanResize;
    Width = Math.Min(InfiniteWidth, area.Width * 0.9);
    Height = Math.Min(InfiniteHeight, area.Height * 0.9);

    // Antes de abrir o CenterScreen cuida disso; trocando de modo depois, a janela cresceria pra fora da tela.
    if (IsLoaded)
    {
      Left = area.Left + ((area.Width - Width) / 2);
      Top = area.Top + ((area.Height - Height) / 2);
    }
  }
}
