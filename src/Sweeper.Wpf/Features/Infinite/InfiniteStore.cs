using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sweeper.Core.Features.Infinite;

namespace Sweeper.Wpf.Features.Infinite;

// Mundo salvo junto com a posição da câmera, pra reabrir o jogo olhando pro mesmo lugar.
public sealed record InfiniteSave(InfiniteSnapshot Game, double CenterX, double CenterY, double CellSize);

public sealed class Preferences
{
  public InfiniteMode InfiniteMode { get; set; } = InfiniteMode.Endless;

  public int BestEndless { get; set; }

  public int BestHardcore { get; set; }

  public int BestTimed { get; set; }

  public int GetBest(InfiniteMode mode) => mode switch
  {
    InfiniteMode.Hardcore => BestHardcore,
    InfiniteMode.Timed => BestTimed,
    _ => BestEndless,
  };

  public void SetBest(InfiniteMode mode, int value)
  {
    switch (mode)
    {
      case InfiniteMode.Hardcore:
        BestHardcore = value;
        break;
      case InfiniteMode.Timed:
        BestTimed = value;
        break;
      default:
        BestEndless = value;
        break;
    }
  }
}

// Arquivos em %LocalAppData%\Sweeper. Erro de disco nunca derruba o jogo: no pior caso começa um mundo novo.
public sealed class InfiniteStore
{
  private readonly string _folder;

  public InfiniteStore()
    : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sweeper"))
  {
  }

  public InfiniteStore(string folder)
  {
    _folder = folder;
    Preferences = Read(PreferencesPath, StoreJsonContext.Default.Preferences) ?? new Preferences();
  }

  public Preferences Preferences { get; }

  private string PreferencesPath => Path.Combine(_folder, "preferences.json");

  public InfiniteSave? Load(InfiniteMode mode) => Read(SavePath(mode), StoreJsonContext.Default.InfiniteSave);

  public void Save(InfiniteMode mode, InfiniteSave save) => Write(SavePath(mode), save, StoreJsonContext.Default.InfiniteSave);

  public void Delete(InfiniteMode mode)
  {
    try
    {
      File.Delete(SavePath(mode));
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
      // Fica o arquivo velho; o próximo save escreve por cima.
    }
  }

  public void SavePreferences() => Write(PreferencesPath, Preferences, StoreJsonContext.Default.Preferences);

  private string SavePath(InfiniteMode mode) => Path.Combine(_folder, $"infinite-{mode.ToString().ToLowerInvariant()}.json");

  private static T? Read<T>(string path, JsonTypeInfo<T> typeInfo)
    where T : class
  {
    try
    {
      if (!File.Exists(path))
      {
        return null;
      }

      using var stream = File.OpenRead(path);
      return JsonSerializer.Deserialize(stream, typeInfo);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
    {
      return null;
    }
  }

  // Escreve num temporário e troca: se o jogo fechar no meio, o save antigo continua inteiro.
  private void Write<T>(string path, T value, JsonTypeInfo<T> typeInfo)
  {
    try
    {
      Directory.CreateDirectory(_folder);
      var temp = path + ".tmp";

      using (var stream = File.Create(temp))
      {
        JsonSerializer.Serialize(stream, value, typeInfo);
      }

      File.Move(temp, path, overwrite: true);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
      // Disco cheio ou pasta bloqueada: segue jogando sem salvar.
    }
  }
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(InfiniteSave))]
[JsonSerializable(typeof(Preferences))]
internal sealed partial class StoreJsonContext : JsonSerializerContext;
