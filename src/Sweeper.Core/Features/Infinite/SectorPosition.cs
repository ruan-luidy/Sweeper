namespace Sweeper.Core.Features.Infinite;

// Coordenada do setor, não da célula: o setor (1, 0) cobre as células x 8..15, y 0..7.
public readonly record struct SectorPosition(int X, int Y);
