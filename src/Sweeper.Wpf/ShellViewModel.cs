using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using Sweeper.Wpf.Features.Infinite;

namespace Sweeper.Wpf;

// Troca a janela entre o modo infinito e o clássico. Os dois ficam vivos: voltar pro clássico não perde a partida.
public sealed partial class ShellViewModel : ObservableObject
{
  // Os modos são criados aqui porque precisam do callback de troca, que aponta pra esta shell.
  public ShellViewModel(
    Func<Action, MainViewModel> createClassic,
    Func<Action, InfiniteViewModel> createInfinite)
  {
    Classic = createClassic(ShowInfinite);
    Infinite = createInfinite(ShowClassic);

    // Sempre abre no infinito; o clássico fica a um clique.
    ShowInfinite();
  }

  public MainViewModel Classic { get; }

  public InfiniteViewModel Infinite { get; }

  [ObservableProperty]
  public partial object Current { get; private set; }

  [ObservableProperty]
  public partial bool IsInfinite { get; private set; }

  // Chamado ao fechar o app.
  public void Save()
  {
    Infinite.Save();
  }

  [MemberNotNull(nameof(Current))]
  private void ShowInfinite()
  {
    Current = Infinite;
    IsInfinite = true;
  }

  [MemberNotNull(nameof(Current))]
  private void ShowClassic()
  {
    Current = Classic;
    IsInfinite = false;
  }
}
