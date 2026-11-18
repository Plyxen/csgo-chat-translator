using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace CsgoChatTranslator;

public enum LinkState { Waiting, Connected, NotAuthorized }

/// <summary>
/// Talks to the game's own network console (-netconport) on this PC.
/// Nothing is injected into the game; this is the same as typing in its console.
/// </summary>
public sealed class GameConsole(int port, string password) : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile NetworkStream? _stream;

    public LinkState State { get; private set; } = LinkState.Waiting;
    public event Action? StateChanged;

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var retry = TimeSpan.FromSeconds(2);
            try
            {
                using var tcp = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeout.CancelAfter(ConnectTimeout);
                    await tcp.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
                }

                var stream = tcp.GetStream();
                var nonce = "csct_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"PASS {password}\necho {nonce}\n"), ct);

                if (await WaitForTextAsync(stream, nonce, ct))
                {
                    _stream = stream;
                    SetState(LinkState.Connected);
                    await DrainAsync(stream, ct);
                    _stream = null;
                    SetState(LinkState.Waiting);
                }
                else
                {
                    // A wrong password prints a line in the game console, so retry slowly.
                    SetState(LinkState.NotAuthorized);
                    retry = TimeSpan.FromSeconds(15);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e) when (e is SocketException or IOException or OperationCanceledException or ObjectDisposedException)
            {
                _stream = null;
                SetState(LinkState.Waiting);
            }

            try
            {
                await Task.Delay(retry, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<bool> SendAsync(string command, CancellationToken ct)
    {
        var stream = _stream;
        if (stream is null) return false;

        await _writeLock.WaitAsync(ct);
        try
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(command + "\n"), ct);
            return true;
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or SocketException)
        {
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static async Task<bool> WaitForTextAsync(NetworkStream stream, string text, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(AuthTimeout);

        var seen = new StringBuilder();
        var buffer = new byte[8192];
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, timeout.Token);
                if (read == 0) throw new IOException("Console closed.");
                seen.Append(Encoding.Latin1.GetString(buffer, 0, read));
                if (seen.ToString().Contains(text, StringComparison.Ordinal)) return true;
                if (seen.Length > 1 << 20) seen.Remove(0, seen.Length - text.Length);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    // The game writes all console output here, so keep reading or its buffer fills up.
    private static async Task DrainAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        while (await stream.ReadAsync(buffer, ct) > 0) { }
    }

    private void SetState(LinkState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke();
    }

    public void Dispose() => _writeLock.Dispose();
}
