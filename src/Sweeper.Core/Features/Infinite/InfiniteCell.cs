using Sweeper.Core.Shared;

namespace Sweeper.Core.Features.Infinite;

// O que a tela pode ver de uma célula. default = fechada, igual a toda célula de setor que ainda não existe.
public readonly record struct InfiniteCell(CellState State, int AdjacentMines, bool IsMine, bool IsExploded);
