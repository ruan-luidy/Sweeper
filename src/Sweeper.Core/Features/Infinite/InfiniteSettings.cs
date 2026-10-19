namespace Sweeper.Core.Features.Infinite;

public sealed record InfiniteSettings(InfiniteMode Mode, int MinesPerSector = InfiniteSettings.DefaultMinesPerSector, TimeSpan? TimeLimit = null)
{
  public const int DefaultMinesPerSector = 10;

  // Com poucas minas os zeros se ligam e uma revelação abriria um pedaço enorme do mapa.
  public const int MinMinesPerSector = 8;

  // Mais que isso deixa o jogo na base do chute.
  public const int MaxMinesPerSector = 20;

  public static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromMinutes(3);

  public static InfiniteSettings Endless { get; } = new(InfiniteMode.Endless);

  public static InfiniteSettings Hardcore { get; } = new(InfiniteMode.Hardcore);

  public static InfiniteSettings Timed { get; } = new(InfiniteMode.Timed, TimeLimit: DefaultTimeLimit);

  public static InfiniteSettings For(InfiniteMode mode) => mode switch
  {
    InfiniteMode.Hardcore => Hardcore,
    InfiniteMode.Timed => Timed,
    _ => Endless,
  };

  // Lista vazia = válido.
  public IReadOnlyList<string> Validate()
  {
    var errors = new List<string>();

    if (MinesPerSector is < MinMinesPerSector or > MaxMinesPerSector)
    {
      errors.Add($"As minas por setor devem estar entre {MinMinesPerSector} e {MaxMinesPerSector}.");
    }

    if (Mode == InfiniteMode.Timed && TimeLimit is null)
    {
      errors.Add("O modo contra o tempo precisa de um limite de tempo.");
    }
    else if (TimeLimit is { } value && value <= TimeSpan.Zero)
    {
      errors.Add("O limite de tempo deve ser maior que zero.");
    }

    return errors;
  }
}
