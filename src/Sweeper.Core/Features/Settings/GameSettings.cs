namespace Sweeper.Core.Features.Settings;

public sealed record GameSettings(int Width, int Height, int MineCount, TimeSpan? TimeLimit = null)
{
  public const int MinSize = 5;
  public const int MaxSize = 50;

  // Primeiro clique + 8 vizinhas nunca têm mina.
  public const int SafeAreaSize = 9;

  public int MaxMines => (Width * Height) - SafeAreaSize;

  // Lista vazia = válido. Devolve mensagens em vez de lançar pra tela de configurações mostrar.
  public IReadOnlyList<string> Validate()
  {
    var errors = new List<string>();

    if (Width is < MinSize or > MaxSize)
    {
      errors.Add($"A largura deve estar entre {MinSize} e {MaxSize}.");
    }

    if (Height is < MinSize or > MaxSize)
    {
      errors.Add($"A altura deve estar entre {MinSize} e {MaxSize}.");
    }

    // Com tamanho inválido o MaxMines não faz sentido.
    if (errors.Count == 0 && (MineCount < 1 || MineCount > MaxMines))
    {
      errors.Add($"As minas devem estar entre 1 e {MaxMines} para um tabuleiro {Width}x{Height}.");
    }

    if (TimeLimit is { } limit && limit <= TimeSpan.Zero)
    {
      errors.Add("O limite de tempo deve ser maior que zero.");
    }

    return errors;
  }

  public bool IsValid => Validate().Count == 0;
}
