using Sweeper.Core.Features.Infinite;
using Sweeper.Core.Shared;

namespace Sweeper.Core.Tests.Infinite;

public sealed class InfiniteGameTests
{
  private static readonly GridPosition Start = new(4, 4);

  [Fact]
  public void PrimeiroCliqueAbreUmZeroComAsVizinhas()
  {
    for (var seed = 0; seed < 50; seed++)
    {
      var game = NewGame(InfiniteSettings.Endless, seed);
      game.Reveal(Start);

      Assert.Equal(InfiniteStatus.Playing, game.Status);
      Assert.Equal(new InfiniteCell(CellState.Revealed, 0, false, false), game.GetCell(Start));
      Assert.All(Around(Start), cell => Assert.Equal(CellState.Revealed, game.GetCell(cell).State));
    }
  }

  [Fact]
  public void MesmaSeedGeraAsMesmasMinasEmQualquerOrdem()
  {
    var first = NewGame(InfiniteSettings.Endless, seed: 7);
    var second = NewGame(InfiniteSettings.Endless, seed: 7);
    first.Reveal(Start);
    second.Reveal(Start);

    var sectors = new[] { new SectorPosition(5, -3), new SectorPosition(-2, 9), new SectorPosition(0, 1) };
    var a = sectors.Select(position => first.GetOrCreateSector(position).Mines).ToList();
    var b = sectors.Reverse().Select(position => second.GetOrCreateSector(position).Mines).Reverse().ToList();

    Assert.Equal(a, b);
    Assert.All(sectors, position => Assert.Equal(InfiniteSettings.DefaultMinesPerSector, first.GetOrCreateSector(position).MineCount));
  }

  [Fact]
  public void CoordenadasNegativasCaemNoSetorCerto()
  {
    Assert.Equal(new SectorPosition(-1, -1), InfiniteGame.SectorOf(new GridPosition(-1, -8)));
    Assert.Equal(new SectorPosition(-2, 0), InfiniteGame.SectorOf(new GridPosition(-9, 7)));
    Assert.Equal(new SectorPosition(1, 0), InfiniteGame.SectorOf(new GridPosition(8, 0)));
  }

  [Fact]
  public void BandeiraAntesDoPrimeiroCliqueNaoFazNada()
  {
    var game = NewGame(InfiniteSettings.Endless);
    game.ToggleFlag(Start);

    Assert.Equal(InfiniteStatus.Ready, game.Status);
    Assert.Equal(CellState.Hidden, game.GetCell(Start).State);
  }

  [Fact]
  public void MinaNoInfinitoTravaOSetorEOJogoContinua()
  {
    var game = NewGame(InfiniteSettings.Endless);
    game.Reveal(Start);

    var target = new SectorPosition(4, 0);
    var mine = MinesOf(game, target).First();
    game.Reveal(mine);

    Assert.Equal(InfiniteStatus.Playing, game.Status);
    Assert.Equal(SectorState.Locked, game.FindSector(target)!.State);
    Assert.Equal(1, game.LockedCount);
    Assert.True(game.GetCell(mine).IsExploded);

    // Setor travado não aceita clique nem bandeira.
    var safe = SafeCellsOf(game, target).First();
    game.Reveal(safe);
    game.ToggleFlag(safe);
    Assert.Equal(CellState.Hidden, game.GetCell(safe).State);
  }

  [Fact]
  public void MinaNoHardcoreAcabaOJogo()
  {
    var game = NewGame(InfiniteSettings.Hardcore);
    game.Reveal(Start);

    var target = new SectorPosition(4, 0);
    game.Reveal(MinesOf(game, target).First());

    Assert.Equal(InfiniteStatus.Lost, game.Status);
    Assert.Equal(0, game.LockedCount);

    // Depois do fim, as minas fechadas aparecem e nada mais abre.
    var otherMine = MinesOf(game, target).Skip(1).First();
    Assert.True(game.GetCell(otherMine).IsMine);
    var safe = SafeCellsOf(game, target).First(cell => game.GetCell(cell).State == CellState.Hidden);
    game.Reveal(safe);
    Assert.Equal(CellState.Hidden, game.GetCell(safe).State);
  }

  [Fact]
  public void AbrirTodasAsCelulasSemMinaResolveOSetor()
  {
    var game = NewGame(InfiniteSettings.Endless);
    game.Reveal(Start);

    var target = new SectorPosition(3, 2);
    SolveSector(game, target);

    var sector = game.FindSector(target)!;
    Assert.Equal(SectorState.Solved, sector.State);
    Assert.True(game.SolvedCount >= 1);

    // As minas que sobraram ganham bandeira.
    Assert.All(MinesOf(game, target), mine => Assert.Equal(CellState.Flagged, game.GetCell(mine).State));
  }

  [Fact]
  public void SetorTravadoDestravaQuandoOsVizinhosSaoResolvidos()
  {
    var game = NewGame(InfiniteSettings.Endless);
    game.Reveal(Start);

    var target = new SectorPosition(5, 5);
    var mine = MinesOf(game, target).First();
    game.Reveal(mine);
    Assert.Equal(SectorState.Locked, game.FindSector(target)!.State);

    var neighbors = NeighborSectors(target).ToList();
    foreach (var neighbor in neighbors.SkipLast(1))
    {
      SolveSector(game, neighbor);
    }

    Assert.Equal(SectorState.Locked, game.FindSector(target)!.State);

    SolveSector(game, neighbors[^1]);

    Assert.NotEqual(SectorState.Locked, game.FindSector(target)!.State);
    Assert.Equal(0, game.LockedCount);
    Assert.Equal(CellState.Flagged, game.GetCell(mine).State);
  }

  [Fact]
  public void TravarSetorJaCercadoDeResolvidosDestravaNaHora()
  {
    // Procura uma seed em que resolver as vizinhas não acaba abrindo o alvo inteiro.
    for (var seed = 0; seed < 50; seed++)
    {
      var game = NewGame(InfiniteSettings.Endless, seed);
      game.Reveal(Start);

      var target = new SectorPosition(5, 5);
      foreach (var neighbor in NeighborSectors(target))
      {
        SolveSector(game, neighbor);
      }

      if (game.FindSector(target)!.State != SectorState.Open)
      {
        continue;
      }

      var mine = MinesOf(game, target).First(cell => game.GetCell(cell).State == CellState.Hidden);
      game.Reveal(mine);

      Assert.Equal(SectorState.Open, game.FindSector(target)!.State);
      Assert.Equal(0, game.LockedCount);
      Assert.Equal(CellState.Flagged, game.GetCell(mine).State);
      return;
    }

    Assert.Fail("Nenhuma seed serviu pro teste.");
  }

  [Fact]
  public void ChordContaMinaExplodidaComoConhecida()
  {
    for (var seed = 0; seed < 200; seed++)
    {
      var game = NewGame(InfiniteSettings.Endless, seed);
      game.Reveal(Start);

      // Um 1 aberto do lado de uma mina: explode a mina e faz chord no 1.
      var target = new SectorPosition(6, 6);
      var mine = MinesOf(game, target).FirstOrDefault(candidate =>
        Around(candidate).Count(cell => IsMine(game, cell)) == 0);
      if (mine == default)
      {
        continue;
      }

      var one = Around(mine).FirstOrDefault(cell =>
        InfiniteGame.SectorOf(cell) != target && Around(cell).Count(near => IsMine(game, near)) == 1);
      if (one == default)
      {
        continue;
      }

      game.Reveal(one);
      game.Reveal(mine);
      game.Reveal(one);

      var closedSafe = Around(one).Where(cell =>
        !IsMine(game, cell) && InfiniteGame.SectorOf(cell) != target);
      Assert.All(closedSafe, cell => Assert.Equal(CellState.Revealed, game.GetCell(cell).State));
      return;
    }

    Assert.Fail("Nenhuma seed serviu pro teste.");
  }

  [Fact]
  public void ContraOTempoAcabaQuandoOTempoPassa()
  {
    var time = new ManualTimeProvider();
    var game = new InfiniteGame(InfiniteSettings.Timed, seed: 1, time);
    game.Reveal(Start);

    time.Advance(InfiniteSettings.DefaultTimeLimit - TimeSpan.FromSeconds(1));
    game.Tick();
    Assert.Equal(InfiniteStatus.Playing, game.Status);
    Assert.Equal(TimeSpan.FromSeconds(1), game.TimeRemaining);

    time.Advance(TimeSpan.FromSeconds(1));
    game.Tick();
    Assert.Equal(InfiniteStatus.TimeUp, game.Status);
    Assert.Equal(TimeSpan.Zero, game.TimeRemaining);

    var hidden = new GridPosition(100, 100);
    game.Reveal(hidden);
    Assert.Equal(CellState.Hidden, game.GetCell(hidden).State);
  }

  [Fact]
  public void SaveRestauraOMesmoMapa()
  {
    var game = NewGame(InfiniteSettings.Endless, seed: 3);
    game.Reveal(Start);
    SolveSector(game, new SectorPosition(2, 0));
    game.Reveal(MinesOf(game, new SectorPosition(-3, 1)).First());
    var flag = SafeCellsOf(game, new SectorPosition(1, 3)).First(cell => game.GetCell(cell).State == CellState.Hidden);
    game.ToggleFlag(flag);

    var restored = InfiniteGame.Restore(game.ToSnapshot(), new ManualTimeProvider());

    Assert.Equal(game.SolvedCount, restored.SolvedCount);
    Assert.Equal(game.LockedCount, restored.LockedCount);
    Assert.Equal(game.FirstClick, restored.FirstClick);
    Assert.Equal(InfiniteStatus.Playing, restored.Status);

    for (var y = -16; y < 40; y++)
    {
      for (var x = -32; x < 40; x++)
      {
        var cell = new GridPosition(x, y);
        Assert.Equal(game.GetCell(cell), restored.GetCell(cell));
      }
    }
  }

  [Fact]
  public void SaveNaoGuardaSetorQueSoFoiGeradoPraContagem()
  {
    var game = NewGame(InfiniteSettings.Endless);
    game.Reveal(Start);
    game.GetOrCreateSector(new SectorPosition(50, 50));

    Assert.DoesNotContain(game.ToSnapshot().Sectors, sector => sector is { X: 50, Y: 50 });
  }

  private static InfiniteGame NewGame(InfiniteSettings settings, int seed = 1) =>
    new(settings, seed, new ManualTimeProvider());

  private static bool IsMine(InfiniteGame game, GridPosition cell)
  {
    var sector = game.GetOrCreateSector(InfiniteGame.SectorOf(cell));
    var index = ((cell.Y & 7) * 8) + (cell.X & 7);
    return (sector.Mines & (1UL << index)) != 0;
  }

  private static IEnumerable<GridPosition> CellsOf(SectorPosition sector)
  {
    for (var y = 0; y < Sector.Size; y++)
    {
      for (var x = 0; x < Sector.Size; x++)
      {
        yield return new GridPosition((sector.X * Sector.Size) + x, (sector.Y * Sector.Size) + y);
      }
    }
  }

  private static IEnumerable<GridPosition> MinesOf(InfiniteGame game, SectorPosition sector) =>
    CellsOf(sector).Where(cell => IsMine(game, cell)).ToList();

  private static IEnumerable<GridPosition> SafeCellsOf(InfiniteGame game, SectorPosition sector) =>
    CellsOf(sector).Where(cell => !IsMine(game, cell)).ToList();

  // Só abre células fechadas, pra nunca disparar um chord sem querer.
  private static void SolveSector(InfiniteGame game, SectorPosition sector)
  {
    foreach (var cell in SafeCellsOf(game, sector))
    {
      if (game.GetCell(cell).State == CellState.Hidden)
      {
        game.Reveal(cell);
      }
    }
  }

  private static IEnumerable<GridPosition> Around(GridPosition cell) =>
    from dy in new[] { -1, 0, 1 }
    from dx in new[] { -1, 0, 1 }
    where dx != 0 || dy != 0
    select new GridPosition(cell.X + dx, cell.Y + dy);

  private static IEnumerable<SectorPosition> NeighborSectors(SectorPosition sector) =>
    from dy in new[] { -1, 0, 1 }
    from dx in new[] { -1, 0, 1 }
    where dx != 0 || dy != 0
    select new SectorPosition(sector.X + dx, sector.Y + dy);
}
