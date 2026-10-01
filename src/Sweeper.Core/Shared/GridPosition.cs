namespace Sweeper.Core.Shared;

// X = coluna, Y = linha. Record struct pra ter igualdade por valor (serve de chave em HashSet).
public readonly record struct GridPosition(int X, int Y);