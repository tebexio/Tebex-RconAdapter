using Tebex.Adapters;
using Tebex.API;
using Tebex.RCON.Protocol;
using Tebex.Util;

// Init startup variables
TebexRconAdapter adapter = new TebexRconAdapter();
Type? pluginType = null;
LegacyRconPlugin? plugin = null;
LegacyProtocolManager? protocolManager = new BaseProtocolManager();

var startupKey = "";
var startupGame = "";
var startupHost = "";
var startupPort = "";
var startupPass = "";
var startupDebug = "false";

// Convert command line arguments to a list
List<string> arguments = args.ToList();

// Determine if any command line args have been set
foreach (var arg in arguments)
{
    if (arg.Contains("--key="))
    {
        startupKey = arg.Split("--key=")[1].Trim();
    }
    
    else if (arg.Contains("--game="))
    {
        startupGame = arg.Split("--game=")[1].Trim();
    }
    
    else if (arg.Contains("--host="))
    {
        startupHost = arg.Split("--host=")[1].Trim();
    }
    
    else if (arg.Contains("--port="))
    {
        startupPort = arg.Split("--port=")[1].Trim();
    }
    
    else if (arg.Contains("--password="))
    {
        startupPass = arg.Split("--password=")[1].Trim();
    }

    else if (arg.Contains("help")) // Stop on help, it will be printed later after we get env vars
    {
        break;
    }
    
    else if (arg.Contains("--telnet") || arg.Contains("--battleye"))
    {
        // Pass
    }
    
    else
    {
        Console.WriteLine($"Unrecognized argument: '{arg}'");
    }
}

// Determine if any environment variables have been set
var envKey = Environment.GetEnvironmentVariable("RCON_ADAPTER_KEY");
var envGame = Environment.GetEnvironmentVariable("RCON_ADAPTER_GAME");
var envHost = Environment.GetEnvironmentVariable("RCON_ADAPTER_HOST");
var envPort = Environment.GetEnvironmentVariable("RCON_ADAPTER_PORT");
var envPass = Environment.GetEnvironmentVariable("RCON_ADAPTER_PASSWORD");
var envDebug = Environment.GetEnvironmentVariable("RCON_ADAPTER_DEBUGMODE");
var envIsService = Environment.GetEnvironmentVariable("RCON_ADAPTER_SERVICEMODE");

if (envKey != null)
{
    startupKey = envKey;
}

if (envGame != null)
{
    startupGame = envGame;
}

if (envHost != null)
{
    startupHost = envHost;
}

if (envPort != null)
{
    startupPort = envPort;
}

if (envPass != null)
{
    startupPass = envPass;
}

if (envDebug != null)
{
    startupDebug = envDebug;
}

// Check if user is asking for help
if (arguments.Contains("help"))
{
    Console.WriteLine("Tebex RCON Adapter " + TebexRconAdapter.Version + " | https://tebex.io/");
    Console.WriteLine();
    Console.WriteLine("Example startup command: ");
    Console.WriteLine("  ./TebexRCON --ip=127.0.0.1 --port=12345 --pass=password");
    Console.WriteLine();
    Console.WriteLine("Arguments may also be provided with environment variables, or set in the app's config file.");
    Console.WriteLine();
    Console.WriteLine("Command-line arguments: ");
    Console.WriteLine(" --key={storeKey}         Your webstore's secret key.");
    Console.WriteLine(" --ip={serverIp}          The game server's IP address");
    Console.WriteLine(" --port={serverPort}      Port for RCON connections on the game server");
    Console.WriteLine(" --pass={password}        Password for your game server's RCON console");
    Console.WriteLine("");
    Console.WriteLine("Startup flags: ");
    Console.WriteLine(" --debug                  Show debug logging while running");
    Console.WriteLine("");
    Console.WriteLine("Environment variables (values display if detected and override command line): ");
    Console.WriteLine($" - RCON_ADAPTER_KEY          {envKey ?? "unset"} ");
    Console.WriteLine($" - RCON_ADAPTER_HOST         {envHost ?? "unset"} ");
    Console.WriteLine($" - RCON_ADAPTER_PORT         {envPort ?? "unset"} ");
    Console.WriteLine($" - RCON_ADAPTER_PASSWORD     {envPass ?? "unset"} ");
    Console.WriteLine("");
    return;
}

if (arguments.Contains("--debug"))
{
    startupDebug = "true";
}

// Initialize the adapter's protocol and plugin type, then the API will initialize and boot the adapter.
adapter.SetStartupArguments(startupKey, startupHost, startupPort, startupPass, startupDebug);
TebexApi.Instance.InitAdapter(adapter);

// Transition to command line input
while (true)
{
    // When running as a service, trying to accept input will cause this to run indefinitely. In service mode
    // the adapter creates threads for its needed processes, so here we just wait.
    if (envIsService == "true")
    {
        Thread.Sleep(1000);
        continue;
    }
    
    Console.Write(Ansi.Blue("Tebex>> "));
    var input = Console.ReadLine();

    if (string.IsNullOrEmpty(input)) continue;
    if (input == "exit") return;

    if (input == "clear" || input == "cls")
    {
        Console.Clear(); 
        continue;
    }
    
    if (input.StartsWith("tebex."))
    {
        adapter.HandleTebexCommand(input);
        continue;
    }
    
    // Pass through any input to the underlying RCON connection
    RconConnection rcon = adapter.GetRcon();
    if (rcon.Polls())
    {
        // polling connections will continually output received data to log, so we won't try to receive next and get stuck.
        var command = rcon.Send(input);
        adapter.LogInfo(command.ToString());
    }
    else
    {
        var command = rcon.Send(input);
        var response = rcon.ReceiveNext();
        
        adapter.LogInfo(command.ToString());
        adapter.LogInfo(response.ToString());
    }
}