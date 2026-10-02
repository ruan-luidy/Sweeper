using System.Text.Json.Serialization;

namespace Sweeper.Net.Features.Coop;

// JSON gerado em compilação, sem reflexão. Enum como texto pra dar pra ler a mensagem num sniffer.
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(CoopMessage))]
internal sealed partial class CoopJsonContext : JsonSerializerContext;
