using System.Diagnostics;
using System.Reflection;
using Tebex.Adapters;
using Tebex.API;
using Tebex.Plugins;
using Tebex.RCON.Protocol;

namespace Tebex.Tests;

/// <summary>
/// Runs the real <see cref="TebexRconAdapter"/>, <see cref="RconConnection"/> and <see cref="ArkPlugin"/> against a
/// <see cref="FakeArkRconServer"/>. This runs in a child process (launched by the tests) because the original bug was
/// an unhandled exception in an async void timer, which terminates the whole process.
///
/// Exit code 0 and "HARNESS PASSED" means the scenario completed without issues.
/// </summary>
public static class ArkRestartHarness
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public static int Main(string[] args)
    {
        var scenario = args.Length > 0 ? args[0] : "";
        try
        {
            switch (scenario)
            {
                case "restart":
                    ServerRestartScenario();
                    break;
                case "delivery":
                    ConcurrentDeliveryScenario();
                    break;
                default:
                    Console.Error.WriteLine("usage: <restart|delivery>");
                    return 2;
            }
        }
        catch (HarnessFailure e)
        {
            Log("HARNESS FAILED: " + e.Message);
            return 1;
        }

        Log("HARNESS PASSED");
        return 0;
    }

    /// <summary>
    /// The scenario from the bug report: the adapter is connected to ARK and polling "listplayers" every 5 seconds when
    /// the ARK server restarts. Previously the next poll wrote to the reset socket and the IOException crashed the app.
    /// </summary>
    private static void ServerRestartScenario()
    {
        using var server = new FakeArkRconServer();
        var (adapter, _) = BootAdapter(server);

        // 1: short restart (server back before the next poll)
        // 2: long restart (port still closed when the adapter notices, so reconnect has to retry)
        // 3: server comes back but resets the first connections while still booting
        var restarts = new[]
        {
            (Downtime: TimeSpan.FromSeconds(3), ResetConnections: 0),
            (Downtime: TimeSpan.FromSeconds(12), ResetConnections: 0),
            (Downtime: TimeSpan.FromSeconds(8), ResetConnections: 2),
        };

        for (var restart = 1; restart <= restarts.Length; restart++)
        {
            var (downtime, resets) = restarts[restart - 1];
            // Wait for a poll to complete so the restart happens while the connection is idle between polls,
            // which is exactly when the adapter's next Send() hits the dead socket.
            var polls = server.ListPlayersRequests;
            WaitFor(() => server.ListPlayersRequests > polls, TimeSpan.FromSeconds(15), "a listplayers poll before restart");
            Thread.Sleep(500);

            Log($"--- Restarting ARK server (restart #{restart}), port closed for {downtime.TotalSeconds}s, resetting first {resets} connections after ---");
            var authsBefore = server.SuccessfulAuths;
            var resetsBefore = server.ResetConnections;
            server.Restart(downtime, resets);

            WaitFor(() => server.SuccessfulAuths > authsBefore, TimeSpan.FromSeconds(40), "adapter to reconnect and re-authenticate");
            Log("Adapter reconnected and re-authenticated");
            if (server.ResetConnections - resetsBefore != resets)
            {
                throw new HarnessFailure($"expected {resets} reset connections during boot, saw {server.ResetConnections - resetsBefore}");
            }

            var pollsAfter = server.ListPlayersRequests;
            WaitFor(() => server.ListPlayersRequests > pollsAfter, TimeSpan.FromSeconds(15), "player list polling to resume after reconnect");
            Log("Player list polling resumed");

            // A package delivery must still work after the restart
            var elapsed = DeliverOnlineCommand(adapter, $"GiveItemToPlayer restart{restart}");
            if (elapsed > TimeSpan.FromSeconds(5))
            {
                throw new HarnessFailure($"delivery after restart took {elapsed.TotalSeconds:F1}s");
            }
        }

        // Let a few more poll cycles run to make sure nothing blows up later
        var finalPolls = server.ListPlayersRequests;
        WaitFor(() => server.ListPlayersRequests >= finalPolls + 2, TimeSpan.FromSeconds(20), "continued polling");
    }

    /// <summary>
    /// Package deliveries while the ARK player-list poll runs on its own timer, against a server that takes a moment to
    /// answer "listplayers". Previously both threads used the shared connection without coordination, so one would read
    /// the other's response and deliveries stalled for many seconds, failed, or triggered reconnects.
    /// </summary>
    private static void ConcurrentDeliveryScenario()
    {
        using var server = new FakeArkRconServer { ListPlayersDelay = TimeSpan.FromMilliseconds(1500) };
        var (adapter, _) = BootAdapter(server);

        WaitFor(() => server.ListPlayersRequests > 0, TimeSpan.FromSeconds(15), "first listplayers poll");
        Thread.Sleep(server.ListPlayersDelay + TimeSpan.FromMilliseconds(500)); // let the first player list arrive

        var slowest = TimeSpan.Zero;
        var connectionsBefore = server.Connections;
        for (var i = 1; i <= 12; i++)
        {
            var elapsed = DeliverOnlineCommand(adapter, $"GiveItemToPlayer item{i}");
            if (elapsed > slowest) slowest = elapsed;
            Thread.Sleep(700);
        }

        Log($"Slowest delivery: {slowest.TotalSeconds:F1}s, reconnects during deliveries: {server.Connections - connectionsBefore}");
        if (slowest > TimeSpan.FromSeconds(5))
        {
            throw new HarnessFailure($"a delivery took {slowest.TotalSeconds:F1}s");
        }
        if (server.Connections != connectionsBefore)
        {
            throw new HarnessFailure("connection was dropped/reconnected during normal deliveries");
        }
    }

    private static TimeSpan DeliverOnlineCommand(TebexRconAdapter adapter, string commandText)
    {
        var player = new TebexApi.DuePlayer
        {
            Id = 1,
            Name = FakeArkRconServer.PlayerName,
            UUID = FakeArkRconServer.PlayerSteamId
        };

        if (!adapter.IsPlayerOnline(player))
        {
            throw new HarnessFailure("player should be detected as online from the ARK player list");
        }

        var command = new TebexApi.Command { Id = 1, CommandToRun = commandText };
        var timer = Stopwatch.StartNew();
        var success = adapter.ExecuteOnlineCommand(command, player, commandText.Split(' ')[0], Array.Empty<string>());
        timer.Stop();

        Log($"Delivered '{commandText}' in {timer.Elapsed.TotalMilliseconds:F0}ms (success={success})");
        if (!success)
        {
            throw new HarnessFailure($"delivery of '{commandText}' failed");
        }
        return timer.Elapsed;
    }

    /// <summary>
    /// Sets up the adapter the same way Init() does after the Tebex API has validated the store, minus the web calls.
    /// </summary>
    private static (TebexRconAdapter, ArkPlugin) BootAdapter(FakeArkRconServer server)
    {
        var adapter = new TebexRconAdapter();
        BaseTebexAdapter.PluginConfig = new BaseTebexAdapter.TebexConfig
        {
            AutoReportingEnabled = false, // don't send plugin events to Tebex from tests
            DebugMode = false,
        };
        SetPrivate(adapter, "_logger", TextWriter.Null); // adapter already echoes logs to the console
        typeof(BaseTebexAdapter).GetProperty("Instance")!.SetValue(null, adapter);

        var plugin = new ArkPlugin(adapter); // starts the 5 second listplayers timer, like in production
        var rcon = plugin.CreateRconConnection("127.0.0.1", server.Port, FakeArkRconServer.Password);
        SetPrivate(adapter, "_plugin", plugin);
        SetPrivate(adapter, "_rcon", rcon);

        var result = rcon.Connect();
        if (!result.Item1)
        {
            throw new HarnessFailure("initial connect failed: " + result.Item2);
        }
        Log($"Connected to fake ARK RCON server on port {server.Port}");
        return (adapter, plugin);
    }

    private static void SetPrivate(object target, string field, object value)
    {
        var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                   ?? throw new HarnessFailure($"field {field} not found");
        info.SetValue(target, value);
    }

    private static void WaitFor(Func<bool> condition, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new HarnessFailure("timed out waiting for " + description);
            }
            Thread.Sleep(50);
        }
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[harness +{Clock.Elapsed.TotalSeconds:F1}s] {message}");
    }

    private sealed class HarnessFailure : Exception
    {
        public HarnessFailure(string message) : base(message) { }
    }
}
