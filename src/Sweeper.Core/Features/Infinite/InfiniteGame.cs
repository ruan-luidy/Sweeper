using Sweeper.Core.Shared;

namespace Sweeper.Core.Features.Infinite;

// Campo infinito dividido em setores 8x8. Os setores só passam a existir quando alguma jogada precisa
// deles, então a memória cresce com o que o jogador explorou, não com o tamanho do mapa.
public sealed class InfiniteGame
{
  private readonly Dictionary<SectorPosition, Sector> _sectors = [];
  private readonly TimeProvider _timeProvider;
  private long _startTimestamp;
  private TimeSpan _finalElapsed;

  public InfiniteGame(InfiniteSettings settings, int seed, TimeProvider timeProvider)
  {
    var errors = settings.Validate();
    if (errors.Count > 0)
    {
      throw new ArgumentException(string.Join(" ", errors), nameof(settings));
    }

    Settings = settings;
    Seed = seed;
    _timeProvider = timeProvider;
  }

  // Sem detalhe de posição: a tela redesenha só o que está visível de qualquer jeito.
  public event Action? Changed;

  public InfiniteSettings Settings { get; }

  public int Seed { get; }

  public InfiniteStatus Status { get; private set; } = InfiniteStatus.Ready;

  public bool IsOver => Status is InfiniteStatus.Lost or InfiniteStatus.TimeUp;

  // As minas de todo o mapa dependem dele (área segura), por isso fica no save.
  public GridPosition? FirstClick { get; private set; }

  // A pontuação: um ponto por setor resolvido.
  public int SolvedCount { get; private set; }

  public int LockedCount { get; private set; }

  public TimeSpan Elapsed => Status switch
  {
    InfiniteStatus.Ready => TimeSpan.Zero,
    InfiniteStatus.Playing => _timeProvider.GetElapsedTime(_startTimestamp),
    _ => _finalElapsed,
  };

  public TimeSpan? TimeRemaining => Settings.TimeLimit is { } limit
    ? (limit > Elapsed ? limit - Elapsed : TimeSpan.Zero)
    : null;

  public static SectorPosition SectorOf(GridPosition cell) =>
    new(cell.X >> 3, cell.Y >> 3);

  // null = setor que ainda não existe (tudo fechado).
  public Sector? FindSector(SectorPosition position) => _sectors.GetValueOrDefault(position);

  // Só lê: nunca cria setor, pra desenhar a tela não gerar mapa.
  public InfiniteCell GetCell(GridPosition cell)
  {
    if (!_sectors.TryGetValue(SectorOf(cell), out var sector))
    {
      return default;
    }

    var index = LocalIndex(cell);
    var bit = Sector.Bit(index);
    var isMine = (sector.Mines & bit) != 0;

    if ((sector.Revealed & bit) != 0)
    {
      return isMine
        ? new InfiniteCell(CellState.Revealed, 0, IsMine: true, IsExploded: true)
        : new InfiniteCell(CellState.Revealed, sector.GetAdjacent(index), IsMine: false, IsExploded: false);
    }

    var state = (sector.Flagged & bit) != 0 ? CellState.Flagged : CellState.Hidden;

    // Mina fechada só aparece quando o Hardcore acaba.
    return new InfiniteCell(state, 0, isMine && Status == InfiniteStatus.Lost, IsExploded: false);
  }

  // Abre a célula; em número já aberto faz o chord.
  public void Reveal(GridPosition position)
  {
    CheckTimeLimit();

    if (IsOver)
    {
      return;
    }

    if (Status == InfiniteStatus.Ready)
    {
      FirstClick = position;
      _startTimestamp = _timeProvider.GetTimestamp();
      Status = InfiniteStatus.Playing;
    }

    var sector = GetOrCreateSector(SectorOf(position));
    if (sector.State == SectorState.Locked)
    {
      return;
    }

    var bit = Sector.Bit(LocalIndex(position));
    var touched = new HashSet<Sector>();

    if ((sector.Revealed & bit) != 0)
    {
      Chord(position, touched);
    }
    else if ((sector.Flagged & bit) == 0)
    {
      Flood(position, touched);
    }

    Settle(touched);
  }

  public void ToggleFlag(GridPosition position)
  {
    CheckTimeLimit();

    // Antes do primeiro clique as minas não podem ser sorteadas: falta saber a área segura.
    if (Status != InfiniteStatus.Playing)
    {
      return;
    }

    var sector = GetOrCreateSector(SectorOf(position));
    var bit = Sector.Bit(LocalIndex(position));

    if (sector.State != SectorState.Open || (sector.Revealed & bit) != 0)
    {
      return;
    }

    sector.Flagged ^= bit;
    Changed?.Invoke();
  }

  // Chamado pelo timer da UI; o Core não tem timer próprio.
  public void Tick() => CheckTimeLimit();

  public InfiniteSnapshot ToSnapshot()
  {
    var sectors = _sectors.Values
      .Where(sector => sector.State != SectorState.Open || (sector.Revealed | sector.Flagged) != 0)
      .Select(sector => new SectorSnapshot(sector.Position.X, sector.Position.Y, sector.State, sector.Revealed, sector.Flagged))
      .ToList();

    return new InfiniteSnapshot(Settings.Mode, Settings.MinesPerSector, Seed, FirstClick, Status, sectors);
  }

  // Sem limite de tempo: o relógio do contra o tempo não sobrevive a fechar o jogo.
  public static InfiniteGame Restore(InfiniteSnapshot snapshot, TimeProvider timeProvider)
  {
    if (snapshot.Mode == InfiniteMode.Timed)
    {
      throw new ArgumentException("O modo contra o tempo não é salvo.", nameof(snapshot));
    }

    var game = new InfiniteGame(new InfiniteSettings(snapshot.Mode, snapshot.MinesPerSector), snapshot.Seed, timeProvider);

    if (snapshot.FirstClick is not { } firstClick)
    {
      return game;
    }

    game.FirstClick = firstClick;
    game.Status = snapshot.Status == InfiniteStatus.Lost ? InfiniteStatus.Lost : InfiniteStatus.Playing;
    game._startTimestamp = timeProvider.GetTimestamp();

    foreach (var saved in snapshot.Sectors)
    {
      var sector = game.GetOrCreateSector(new SectorPosition(saved.X, saved.Y));
      sector.Revealed = saved.Revealed;
      sector.Flagged = saved.Flagged & ~saved.Revealed;
      sector.State = saved.State;
    }

    // Contagens depois de carregar tudo, porque criam os setores vizinhos que faltarem.
    foreach (var sector in game._sectors.Values.ToList())
    {
      var safeRevealed = sector.Revealed & ~sector.Mines;
      for (var index = 0; index < Sector.CellCount; index++)
      {
        if ((safeRevealed & Sector.Bit(index)) != 0)
        {
          sector.SetAdjacent(index, game.CountAdjacentMines(CellAt(sector.Position, index)));
        }
      }

      game.SolvedCount += sector.State == SectorState.Solved ? 1 : 0;
      game.LockedCount += sector.State == SectorState.Locked ? 1 : 0;
    }

    return game;
  }

  private void Chord(GridPosition position, HashSet<Sector> touched)
  {
    var count = GetCell(position).AdjacentMines;
    if (count <= 0)
    {
      return;
    }

    var neighbors = Neighbors(position).ToList();

    // Mina explodida em setor travado também conta como mina conhecida.
    var known = neighbors.Count(neighbor => GetCell(neighbor) is { State: CellState.Flagged } or { IsExploded: true });
    if (known != count)
    {
      return;
    }

    // Flood já ignora bandeiras, células abertas e setores travados.
    foreach (var neighbor in neighbors)
    {
      Flood(neighbor, touched);
    }
  }

  // BFS com fila em vez de recursão: num mapa infinito a pilha não aguentaria.
  private void Flood(GridPosition start, HashSet<Sector> touched)
  {
    if (!TryRevealCell(start, touched, out var isZero) || !isZero)
    {
      return;
    }

    var queue = new Queue<GridPosition>();
    queue.Enqueue(start);

    while (queue.TryDequeue(out var current))
    {
      foreach (var neighbor in Neighbors(current))
      {
        if (TryRevealCell(neighbor, touched, out var neighborIsZero) && neighborIsZero)
        {
          queue.Enqueue(neighbor);
        }
      }
    }
  }

  private bool TryRevealCell(GridPosition position, HashSet<Sector> touched, out bool isZero)
  {
    isZero = false;

    if (IsOver)
    {
      return false;
    }

    var sector = GetOrCreateSector(SectorOf(position));
    var index = LocalIndex(position);
    var bit = Sector.Bit(index);

    if (sector.State == SectorState.Locked || ((sector.Revealed | sector.Flagged) & bit) != 0)
    {
      return false;
    }

    sector.Revealed |= bit;
    touched.Add(sector);

    if ((sector.Mines & bit) != 0)
    {
      HitMine(sector);
      return true;
    }

    var count = CountAdjacentMines(position);
    sector.SetAdjacent(index, count);
    isZero = count == 0;
    return true;
  }

  private void HitMine(Sector sector)
  {
    if (Settings.Mode == InfiniteMode.Hardcore)
    {
      Finish(InfiniteStatus.Lost);
      return;
    }

    sector.State = SectorState.Locked;
    LockedCount++;
  }

  // Resolve os setores que ficaram completos e destrava os vizinhos que ficaram cercados de resolvidos.
  private void Settle(HashSet<Sector> touched)
  {
    if (touched.Count == 0)
    {
      return;
    }

    var queue = new Queue<Sector>(touched);

    // Travou agora num setor já cercado de resolvidos: nenhum vizinho vai ser resolvido pra destravar depois.
    foreach (var sector in touched)
    {
      if (sector.State == SectorState.Locked && CanUnlock(sector))
      {
        Unlock(sector);
      }
    }

    while (queue.TryDequeue(out var sector))
    {
      // Setor completado na mesma jogada que perdeu o Hardcore não vale ponto.
      if (sector.State != SectorState.Open || !sector.AllSafeRevealed || Status == InfiniteStatus.Lost)
      {
        continue;
      }

      Solve(sector);

      foreach (var neighbor in NeighborSectors(sector.Position))
      {
        if (FindSector(neighbor) is { State: SectorState.Locked } locked && CanUnlock(locked))
        {
          Unlock(locked);
          queue.Enqueue(locked);
        }
      }
    }

    Changed?.Invoke();
  }

  private void Solve(Sector sector)
  {
    sector.State = SectorState.Solved;

    // Igual à vitória do clássico: as minas que sobraram ganham bandeira.
    sector.Flagged |= sector.Mines & ~sector.Revealed;
    SolvedCount++;
  }

  // Travado aceita vizinho travado: senão dois travados lado a lado se prenderiam pra sempre.
  // Só é conferido quando um vizinho é resolvido, então travar um setor nunca destrava outro.
  private bool CanUnlock(Sector locked) =>
    NeighborSectors(locked.Position).All(neighbor => FindSector(neighbor) is { State: not SectorState.Open });

  // A mina explodida vira bandeira e o setor volta a ser jogável do jeito que estava.
  private void Unlock(Sector sector)
  {
    var exploded = sector.Revealed & sector.Mines;
    sector.Revealed &= ~exploded;
    sector.Flagged |= exploded;
    sector.State = SectorState.Open;
    LockedCount--;
  }

  private void CheckTimeLimit()
  {
    if (Status != InfiniteStatus.Playing || Settings.TimeLimit is not { } limit || Elapsed < limit)
    {
      return;
    }

    Finish(InfiniteStatus.TimeUp);
    Changed?.Invoke();
  }

  // Congela o tempo antes de trocar o status, porque Elapsed depende dele.
  private void Finish(InfiniteStatus status)
  {
    _finalElapsed = _timeProvider.GetElapsedTime(_startTimestamp);
    Status = status;
  }

  internal Sector GetOrCreateSector(SectorPosition position)
  {
    if (!_sectors.TryGetValue(position, out var sector))
    {
      // Só é chamado depois do primeiro clique (Reveal e ToggleFlag garantem).
      var safeCenter = FirstClick ?? throw new InvalidOperationException("O primeiro clique ainda não aconteceu.");
      sector = new Sector(position, SectorGenerator.GenerateMines(Seed, position, Settings.MinesPerSector, safeCenter));
      _sectors.Add(position, sector);
    }

    return sector;
  }

  private int CountAdjacentMines(GridPosition position)
  {
    var count = 0;
    foreach (var neighbor in Neighbors(position))
    {
      var sector = GetOrCreateSector(SectorOf(neighbor));
      if ((sector.Mines & Sector.Bit(LocalIndex(neighbor))) != 0)
      {
        count++;
      }
    }

    return count;
  }

  private static int LocalIndex(GridPosition cell) =>
    ((cell.Y & (Sector.Size - 1)) * Sector.Size) + (cell.X & (Sector.Size - 1));

  private static GridPosition CellAt(SectorPosition sector, int index) =>
    new((sector.X * Sector.Size) + (index % Sector.Size), (sector.Y * Sector.Size) + (index / Sector.Size));

  private static IEnumerable<GridPosition> Neighbors(GridPosition position)
  {
    for (var dy = -1; dy <= 1; dy++)
    {
      for (var dx = -1; dx <= 1; dx++)
      {
        if (dx != 0 || dy != 0)
        {
          yield return new GridPosition(position.X + dx, position.Y + dy);
        }
      }
    }
  }

  private static IEnumerable<SectorPosition> NeighborSectors(SectorPosition position)
  {
    for (var dy = -1; dy <= 1; dy++)
    {
      for (var dx = -1; dx <= 1; dx++)
      {
        if (dx != 0 || dy != 0)
        {
          yield return new SectorPosition(position.X + dx, position.Y + dy);
        }
      }
    }
  }
}
