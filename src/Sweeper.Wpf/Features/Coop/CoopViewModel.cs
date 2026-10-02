using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sweeper.Net.Features.Coop;

namespace Sweeper.Wpf.Features.Coop;

public sealed partial class CoopViewModel : ObservableObject
{
  // Sem isso, um IP errado fica uns 20 s tentando antes do Windows desistir.
  private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(5);

  private CancellationTokenSource? _cts;

  public CoopViewModel()
  {
    LocalAddresses = CoopConnection.GetLocalAddresses();
  }

  // O primeiro é o mais provável (placa com gateway); os outros ficam de reserva.
  public IReadOnlyList<string> LocalAddresses { get; }

  public bool HasLocalAddress => LocalAddresses.Count > 0;

  [ObservableProperty]
  public partial string CopiedText { get; private set; } = "";

  public int Port => CoopConnection.DefaultPort;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(HostCommand), nameof(JoinCommand))]
  public partial bool IsBusy { get; private set; }

  [ObservableProperty]
  public partial string StatusText { get; private set; } = "";

  [ObservableProperty]
  public partial string ErrorText { get; private set; } = "";

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(JoinCommand))]
  public partial string HostAddress { get; set; } = "";

  public event Action<CoopSession>? Connected;

  public event Action? Cancelled;

  [RelayCommand(CanExecute = nameof(CanHost))]
  private Task Host() => ConnectAsync(
    "Esperando o parceiro entrar...",
    token => CoopConnection.HostAsync(Port, token),
    isHost: true,
    timeout: null);

  [RelayCommand(CanExecute = nameof(CanJoin))]
  private Task Join() => ConnectAsync(
    $"Conectando em {HostAddress.Trim()}...",
    token => CoopConnection.JoinAsync(HostAddress.Trim(), Port, token),
    isHost: false,
    JoinTimeout);

  [RelayCommand]
  private void CopyAddress(string address)
  {
    try
    {
      Clipboard.SetText(address);
      CopiedText = $"{address} copiado, é só mandar pro parceiro.";
    }
    catch (COMException)
    {
      // Outro programa segurando a área de transferência; tenta de novo que costuma ir.
      CopiedText = "Não deu pra copiar agora, tenta de novo.";
    }
  }

  [RelayCommand]
  private void Cancel()
  {
    _cts?.Cancel();
    Cancelled?.Invoke();
  }

  private bool CanHost() => !IsBusy;

  private bool CanJoin() => !IsBusy && !string.IsNullOrWhiteSpace(HostAddress);

  private async Task ConnectAsync(
    string status,
    Func<CancellationToken, Task<CoopConnection>> connect,
    bool isHost,
    TimeSpan? timeout)
  {
    using var cts = new CancellationTokenSource();
    _cts = cts;

    if (timeout is { } limit)
    {
      cts.CancelAfter(limit);
    }

    IsBusy = true;
    ErrorText = "";
    StatusText = status;

    try
    {
      var connection = await connect(cts.Token);
      Connected?.Invoke(new CoopSession(connection, isHost));
    }
    catch (OperationCanceledException)
    {
      // Cancelado pelo botão o diálogo já fechou; aqui sobra o caso do tempo esgotado.
      ErrorText = "Ninguém respondeu nesse IP. Confere se o host já criou a sala.";
    }
    catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
    {
      ErrorText = $"A porta {Port} já está em uso. Outro Sweeper aberto hospedando?";
    }
    catch (SocketException)
    {
      ErrorText = "Não deu pra conectar. Confere o IP e se os dois estão na mesma rede.";
    }
    finally
    {
      _cts = null;
      IsBusy = false;
      StatusText = "";
    }
  }
}
