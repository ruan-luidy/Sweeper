namespace Sweeper.Core.Features.Infinite;

public enum SectorState
{
  Open,

  // Todas as células sem mina abertas: vale ponto.
  Solved,

  // Explodiu uma mina aqui: ninguém mexe até as vizinhas serem resolvidas.
  Locked,
}
