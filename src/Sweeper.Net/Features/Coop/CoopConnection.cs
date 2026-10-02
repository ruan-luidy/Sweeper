using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Sweeper.Net.Features.Coop;

// TCP com uma mensagem JSON por linha. Só transporta; as regras do coop ficam na CoopSession.
public sealed class CoopConnection : IDisposable
{
  public const int DefaultPort = 27015;

  private readonly TcpClient _client;
  private readonly StreamReader _reader;
  private readonly StreamWriter _writer;
  private readonly CancellationTokenSource _cts = new();

  // Fila em vez de escrever direto: a ordem das jogadas tem que chegar igual à ordem do Send.
  private readonly Channel<string> _outgoing = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

  private SynchronizationContext? _context;
  private bool _disposed;

  private CoopConnection(TcpClient client)
  {
    _client = client;
    _client.NoDelay = true;

    var stream = client.GetStream();
    _reader = new StreamReader(stream, Encoding.UTF8);
    _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    RemoteAddress = client.Client.RemoteEndPoint is IPEndPoint endPoint
      ? endPoint.Address.MapToIPv4().ToString()
      : "?";
  }

  public string RemoteAddress { get; }

  public event Action<CoopMessage>? MessageReceived;

  // Não dispara quando a gente mesmo fecha (Dispose).
  public event Action? Disconnected;

  // Espera um parceiro entrar. Cancelar o token para de esperar.
  public static async Task<CoopConnection> HostAsync(int port, CancellationToken cancellationToken)
  {
    var listener = new TcpListener(IPAddress.Any, port);
    listener.Start();

    try
    {
      var client = await listener.AcceptTcpClientAsync(cancellationToken);
      return new CoopConnection(client);
    }
    finally
    {
      // Só um parceiro: depois que ele entra, ninguém mais consegue conectar.
      listener.Stop();
    }
  }

  public static async Task<CoopConnection> JoinAsync(string host, int port, CancellationToken cancellationToken)
  {
    var client = new TcpClient();

    try
    {
      await client.ConnectAsync(host, port, cancellationToken);
      return new CoopConnection(client);
    }
    catch
    {
      client.Dispose();
      throw;
    }
  }

  // IPs desta máquina na rede local, pra mostrar pro host passar pro parceiro.
  // Placa com gateway primeiro: é a do roteador; as virtuais (VirtualBox, Hyper-V, WSL) não têm.
  public static IReadOnlyList<string> GetLocalAddresses()
  {
    try
    {
      return NetworkInterface.GetAllNetworkInterfaces()
        .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                          network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .Select(network => network.GetIPProperties())
        .OrderByDescending(properties => properties.GatewayAddresses.Any(gateway => !gateway.Address.Equals(IPAddress.Any)))
        .SelectMany(properties => properties.UnicastAddresses)
        .Where(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetwork)
        .Select(unicast => unicast.Address.ToString())
        .Distinct()
        .ToList();
    }
    catch (NetworkInformationException)
    {
      return [];
    }
  }

  // Os eventos chegam no SynchronizationContext de quem chamou Start (a thread da UI no WPF).
  public void Start()
  {
    _context = SynchronizationContext.Current;
    _ = ReadLoopAsync();
    _ = WriteLoopAsync();
  }

  public void Send(CoopMessage message)
  {
    if (_disposed)
    {
      return;
    }

    _outgoing.Writer.TryWrite(JsonSerializer.Serialize(message, CoopJsonContext.Default.CoopMessage));
  }

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    _disposed = true;
    _outgoing.Writer.TryComplete();
    _cts.Cancel();
    _client.Dispose();
  }

  private async Task ReadLoopAsync()
  {
    try
    {
      while (await _reader.ReadLineAsync(_cts.Token).ConfigureAwait(false) is { } line)
      {
        var message = JsonSerializer.Deserialize(line, CoopJsonContext.Default.CoopMessage);
        if (message is not null)
        {
          Raise(() => MessageReceived?.Invoke(message));
        }
      }
    }
    catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or JsonException)
    {
      // Conexão caiu ou mensagem quebrada: nos dois casos a partida coop acabou.
    }

    Raise(() =>
    {
      if (!_disposed)
      {
        Disconnected?.Invoke();
      }
    });
  }

  private async Task WriteLoopAsync()
  {
    try
    {
      await foreach (var line in _outgoing.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
      {
        await _writer.WriteLineAsync(line).ConfigureAwait(false);
        await _writer.FlushAsync(_cts.Token).ConfigureAwait(false);
      }
    }
    catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
    {
      // Quem avisa a queda é o loop de leitura.
    }
  }

  private void Raise(Action action)
  {
    if (_context is null)
    {
      action();
    }
    else
    {
      _context.Post(_ => action(), null);
    }
  }
}
