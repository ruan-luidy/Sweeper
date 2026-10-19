using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using Sweeper.Wpf.Features.Infinite;

namespace Sweeper.Wpf;

// Troca a janela entre o modo infinito e o clássico. Os dois ficam vivos: voltar pro clássico não perde a partida.
public sealed partial class ShellViewModel : ObservableObject
{
  private readonly InfiniteStore _store;

  // Os modos são criados aqui porque precisam do callback de troca, que aponta pra esta shell.
  public ShellViewModel(
    InfiniteStore store,
    Func<Action, MainViewModel> createClassic,
    Func<Action, InfiniteViewModel> createInfinite)
  {
    _store = store;
    Classic = createClassic(ShowInfinite);
    Infinite = createInfinite(ShowClassic);

    if (store.Preferences.StartInClassic)
    {
      ShowClassic();
    }
    else
    {
      ShowInfinite();
    }
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
    _store.Preferences.StartInClassic = !IsInfinite;
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
