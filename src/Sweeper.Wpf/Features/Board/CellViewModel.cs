using CommunityToolkit.Mvvm.ComponentModel;
using Sweeper.Core.Shared;

namespace Sweeper.Wpf.Features.Board;

public sealed partial class CellViewModel : ObservableObject
{
  public CellViewModel(GridPosition position)
  {
    Position = position;
  }

  public GridPosition Position { get; }

  [ObservableProperty]
  public partial CellState State { get; private set; }

  [ObservableProperty]
  public partial int AdjacentMines { get; private set; }

  [ObservableProperty]
  public partial bool IsMine { get; private set; }

  [ObservableProperty]
  public partial bool IsExploded { get; private set; }

  // Bandeira em célula sem mina, revelada no fim do jogo.
  [ObservableProperty]
  public partial bool IsWrongFlag { get; private set; }

  // Vizinha fechada do número sob o mouse: onde a bomba pode estar.
  [ObservableProperty]
  public partial bool IsInRange { get; set; }

  // O próprio número sob o mouse, centro do range.
  [ObservableProperty]
  public partial bool IsRangeCenter { get; set; }

  // Célula sob o mouse do parceiro no coop.
  [ObservableProperty]
  public partial bool IsPartnerHover { get; set; }

  // Só passa pra UI o que o jogador pode ver: número só em célula aberta, mina só no fim do jogo.
  public void Update(Cell cell, bool isGameOver)
  {
    State = cell.State;
    AdjacentMines = cell.State == CellState.Revealed && !cell.IsMine ? cell.AdjacentMines : 0;
    IsMine = isGameOver && cell.IsMine;
    IsExploded = cell.IsMine && cell.State == CellState.Revealed;
    IsWrongFlag = isGameOver && cell.State == CellState.Flagged && !cell.IsMine;
  }
}
