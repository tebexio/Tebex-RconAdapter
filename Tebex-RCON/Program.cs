using Tebex.Adapters;
using Tebex.API;
using Tebex.Plugins;
using Tebex.RCON.Protocol;
using Tebex.Util;

// Init startup variables
TebexRconAdapter adapter = new TebexRconAdapter();
Type? pluginType = null;
TebexRconPlugin? plugin = null;
ProtocolManagerBase? protocolManager = new StdProtocolManager();

var startupKey = "";
var startupGame = "";
var startupHost = "";
var startupPort = "";
var startupPass = "";
var startupDebug = "false";

// Dictionary containing valid plugin types that are available
Dictionary<string, Type> pluginTypes = new Dictionary<string, Type>()
{
    {"7d2d", typeof(SevenDaysPlugin) },
    {"conanexiles", typeof(ConanExilesPlugin) },
    {"dayz", typeof(DayZPlugin) },
    {"projectzomboid", typeof(ProjectZomboidPlugin)},
    {"minecraft", typeof(MinecraftPlugin)},
    {"arkse", typeof(ArkPlugin)},
    {"rust", typeof(RustPlugin)}
};

List<string> pluginsAvailable = pluginTypes.Keys.ToList();

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
    Console.WriteLine("  ./TebexRCON --game=7d2d --ip=127.0.0.1 --port=12345 --pass=password --telnet");
    Console.WriteLine();
    Console.WriteLine("Arguments may also be provided with environment variables, or set in the app's config file.");
    Console.WriteLine();
    Console.WriteLine("Command-line arguments: ");
    Console.WriteLine(" --key={storeKey}         Your webstore's secret key.");
    Console.WriteLine(" --game={gameName}        The game plugin you wish to run. See available plugins below.");
    Console.WriteLine(" --ip={serverIp}          The game server's IP address");
    Console.WriteLine(" --port={serverPort}      Port for RCON connections on the game server");
    Console.WriteLine(" --pass={password}        Password for your game server's RCON console");
    Console.WriteLine("");
    Console.WriteLine("Startup flags: ");
    Console.WriteLine(" --telnet                 Uses telnet protocol instead of RCON");
    Console.WriteLine(" --debug                  Show debug logging while running");
    Console.WriteLine("");
    Console.WriteLine("Available plugins: ");
    foreach (var plugName in pluginsAvailable)
    {
        Console.WriteLine($" - {plugName}");
    }
    Console.WriteLine("");
    Console.WriteLine("Environment variables (values display if detected and override command line): ");
    Console.WriteLine($" - RCON_ADAPTER_KEY          {envKey ?? "unset"} ");
    Console.WriteLine($" - RCON_ADAPTER_GAME         {envGame ?? "unset"} ");
    Console.WriteLine($" - RCON_ADAPTER_HOST         {envHost ?? "unset"} ");
    Console.WriteLine($" - RCON_ADAPTER_PORT         {envPort ?? "unset"} ");
    Console.WriteLine($" - RCON_ADAPTER_PASSWORD     {envPass ?? "unset"} ");
    Console.WriteLine("");
    return;
}

// Determine what game we want by using the first name detected on the command line in our list of plugins available.
foreach (var plugName in pluginsAvailable)
{
    if (startupGame == plugName)
    {
        pluginType = pluginTypes[plugName];
        break;
    }
}

// Handle bad plugin type selection
if (pluginType == null)
{
    Console.WriteLine($"No plugin for game '{startupGame}', please provide your desired plugin as a launch argument or enter it below: ");
    Console.WriteLine("Available plugins: ");
    foreach (var plugName in pluginsAvailable)
    {
        Console.WriteLine($" - '{plugName}'");
    }
    
    Console.WriteLine("Enter which plugin you want to run: ");
    
    while (true) // Ask the user which plugin to run until they quit.
    {
        Console.Write(Ansi.Blue("Tebex>> "));
        var desiredPlugin = Console.ReadLine();
        if (desiredPlugin != null && pluginsAvailable.Contains(desiredPlugin))
        {
            startupGame = desiredPlugin;
            break;
        }

        if (desiredPlugin != null && desiredPlugin.Equals("exit"))
        {
            return;
        }
    }
}

// Check command line flags
if (arguments.Contains("--telnet"))
{
    protocolManager = new TelnetProtocolManager();
}

if (arguments.Contains("--debug")) 
{
        
}

if (arguments.Contains("--battleye") || startupGame.Equals("dayz"))
{
    protocolManager = new BattleNetProtocolManager();
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
}

if (startupGame.Equals("minecraft") || startupGame.Equals("arkse"))
{
    protocolManager = new MinecraftProtocolManager();
}

if (startupGame.Equals("arkse"))
{
    protocolManager = new ArkSeProtocolManager();
}

if (startupGame.Equals("rust"))
{
    protocolManager = new WebsocketProtocolManager();
}

// Initialize the adapter's protocol and plugin type, then the API will initialize and boot the adapter.
adapter.SetProtocol(protocolManager);
adapter.SetPluginType(pluginType);
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
    if (adapter.GetProtocol() != null)
    {
        if (!adapter.GetProtocol().IsConnected())
        {
            Console.WriteLine("Tebex is not connected.");
            continue;
        }
        
        adapter.GetProtocol()?.Write(input); //missing "2" for auth
        var response = adapter.GetProtocol()?.Read();
        Console.WriteLine(response);
        continue;
    }
    
    Console.WriteLine("Unrecognized command. If you ran a server command, we are not connected to a game server.");
    Console.WriteLine("Please check your secret key and server connection.");
}