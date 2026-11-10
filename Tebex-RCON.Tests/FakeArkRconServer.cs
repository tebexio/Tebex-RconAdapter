using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Tebex.Tests;

/// <summary>
/// Minimal Source-RCON server that behaves like an ARK: Survival Evolved server for the purposes of the adapter:
/// it authenticates, answers "listplayers" with a player list and answers anything else like ARK does
/// ("Server received, But no response!!"). <see cref="Restart"/> simulates an ARK server restart by resetting every
/// client connection (TCP RST, which produces "An existing connection was forcibly closed by the remote host") and
/// keeping the port closed for a while before accepting connections again.
/// </summary>
public sealed class FakeArkRconServer : IDisposable
{
    public const string Password = "test-password";
    public const string PlayerName = "TestSurvivor";
    public const string PlayerSteamId = "76561198000000001";

    private const int AuthResponse = 2;
    private const int ResponseValue = 0;
    private const int LoginRequest = 3;

    private readonly ConcurrentDictionary<TcpClient, byte> _clients = new();
    private TcpListener _listener;
    private volatile bool _running;

    public int Port { get; }

    /// <summary>
    /// How long the server takes to answer "listplayers". Real ARK servers can take a noticeable amount of time with
    /// many players or under load, which widens the window for request/response interleaving.
    /// </summary>
    public TimeSpan ListPlayersDelay = TimeSpan.Zero;

    /// <summary>
    /// Number of upcoming connections to accept and immediately reset, simulating a server that has opened its RCON
    /// port but is still booting.
    /// </summary>
    public int ResetNextConnections;
    public int ResetConnections;
    public int Connections;
    public int SuccessfulAuths;
    public int ListPlayersRequests;
    public int OtherCommands;
    public readonly ConcurrentQueue<string> ReceivedCommands = new();

    public FakeArkRconServer()
    {
        // Grab a free port, then keep using that port across restarts
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        Port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        _listener = CreateListener();
        Start();
    }

    private TcpListener CreateListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, Port);
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        return listener;
    }

    public void Start()
    {
        _listener = CreateListener();
        _listener.Start();
        _running = true;
        _ = AcceptLoop(_listener);
    }

    /// <summary>
    /// Simulates the game server going down: all RCON clients are reset and the port is closed for <paramref name="downtime"/>.
    /// </summary>
    public void Restart(TimeSpan downtime, int resetConnectionsAfterRestart = 0)
    {
        _running = false;
        _listener.Stop();
        foreach (var client in _clients.Keys)
        {
            try
            {
                // Linger 0 makes Close() send RST instead of FIN, exactly like a crashed/killed server process
                client.Client.LingerState = new LingerOption(true, 0);
                client.Close();
            }
            catch (Exception) { /* ignored */ }
        }
        _clients.Clear();

        Thread.Sleep(downtime);
        ResetNextConnections = resetConnectionsAfterRestart;
        Start();
    }

    private async Task AcceptLoop(TcpListener listener)
    {
        while (_running)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync();
            }
            catch (Exception)
            {
                return; // listener stopped
            }

            Interlocked.Increment(ref Connections);
            if (Interlocked.Decrement(ref ResetNextConnections) >= 0)
            {
                Interlocked.Increment(ref ResetConnections);
                client.Client.LingerState = new LingerOption(true, 0);
                client.Close();
                continue;
            }
            Interlocked.Exchange(ref ResetNextConnections, 0);
            _clients[client] = 0;
            _ = Task.Run(() => HandleClient(client));
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var authenticated = false;
            while (true)
            {
                var sizeBytes = ReadExactly(stream, 4);
                var size = BitConverter.ToInt32(sizeBytes, 0);
                var body = ReadExactly(stream, size);
                var id = BitConverter.ToInt32(body, 0);
                var type = BitConverter.ToInt32(body, 4);
                var message = Encoding.UTF8.GetString(body, 8, Math.Max(0, size - 10));

                if (type == LoginRequest)
                {
                    authenticated = message == Password;
                    if (authenticated)
                    {
                        Interlocked.Increment(ref SuccessfulAuths);
                    }
                    Write(stream, authenticated ? id : -1, AuthResponse, "");
                    continue;
                }

                if (!authenticated)
                {
                    return;
                }

                ReceivedCommands.Enqueue(message);
                if (message == "listplayers")
                {
                    Interlocked.Increment(ref ListPlayersRequests);
                    if (ListPlayersDelay > TimeSpan.Zero)
                    {
                        Thread.Sleep(ListPlayersDelay);
                    }
                    Write(stream, id, ResponseValue, $"\n0. {PlayerName}, {PlayerSteamId}\n ");
                }
                else
                {
                    Interlocked.Increment(ref OtherCommands);
                    Write(stream, id, ResponseValue, "Server received, But no response!! \n ");
                }
            }
        }
        catch (Exception)
        {
            // client went away or was reset by Restart()
        }
        finally
        {
            _clients.TryRemove(client, out _);
        }
    }

    private static byte[] ReadExactly(NetworkStream stream, int count)
    {
        var buffer = new byte[count];
        stream.ReadExactly(buffer, 0, count);
        return buffer;
    }

    private static void Write(NetworkStream stream, int id, int type, string message)
    {
        var payload = Encoding.UTF8.GetBytes(message);
        var packet = new byte[14 + payload.Length];
        BitConverter.GetBytes(packet.Length - 4).CopyTo(packet, 0);
        BitConverter.GetBytes(id).CopyTo(packet, 4);
        BitConverter.GetBytes(type).CopyTo(packet, 8);
        payload.CopyTo(packet, 12);
        stream.Write(packet, 0, packet.Length); // single write so the adapter's single Read() gets the whole packet
    }

    public void Dispose()
    {
        _running = false;
        try { _listener.Stop(); } catch (Exception) { /* ignored */ }
        foreach (var client in _clients.Keys)
        {
            try { client.Close(); } catch (Exception) { /* ignored */ }
        }
    }
}
