using Sweeper.Core.Shared;

namespace Sweeper.Core.Features.Infinite;

// As minas de um setor saem só da seed e da posição dele: a ordem em que os setores são criados não muda o
// mapa, e o save não precisa guardar as minas. SplitMix64 próprio em vez de Random pra não depender da versão do .NET.
internal static class SectorGenerator
{
  public static ulong GenerateMines(int seed, SectorPosition position, int mineCount, GridPosition safeCenter)
  {
    var state = Mix((uint)seed ^ Mix(((ulong)(uint)position.X << 32) | (uint)position.Y));

    // Primeiro clique + 8 vizinhas nunca têm mina, igual ao clássico.
    Span<int> candidates = stackalloc int[Sector.CellCount];
    var count = 0;
    for (var i = 0; i < Sector.CellCount; i++)
    {
      var x = (position.X * Sector.Size) + (i % Sector.Size);
      var y = (position.Y * Sector.Size) + (i / Sector.Size);
      if (Math.Abs(x - safeCenter.X) <= 1 && Math.Abs(y - safeCenter.Y) <= 1)
      {
        continue;
      }

      candidates[count++] = i;
    }

    // Fisher-Yates parcial: sorteio sem repetição.
    ulong mines = 0;
    var total = Math.Min(mineCount, count);
    for (var i = 0; i < total; i++)
    {
      state += 0x9E3779B97F4A7C15;
      var j = i + (int)(Mix(state) % (ulong)(count - i));
      (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
      mines |= Sector.Bit(candidates[i]);
    }

    return mines;
  }

  private static ulong Mix(ulong z)
  {
    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
    z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
    return z ^ (z >> 31);
  }
}
