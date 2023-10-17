using Tebex.Adapters;
using Tebex.API;
using Tebex.Plugins;
using Tebex.RCON.Protocol;

TebexRconAdapter adapter = new TebexRconAdapter();
Type? pluginType = null;
TebexRconPlugin? plugin = null;
ProtocolManagerBase? protocolManager = new StdProtocolManager();

Dictionary<string, Type> pluginTypes = new Dictionary<string, Type>()
{
    {"7d2d", typeof(SevenDaysPlugin) },
    {"conanexiles", typeof(ConanExilesPlugin) },
};
List<string> pluginsAvailable = pluginTypes.Keys.ToList();

// Store command line args in a list and check for telnet
List<string> arguments = args.ToList();
if (arguments.Contains("--telnet"))
{
    protocolManager = new TelnetProtocolManager();
}

// Determine what game we want by using the first name detected on the command line in our list of plugins available.
foreach (var plugName in pluginsAvailable)
{
    if (arguments.Contains(plugName))
    {
        pluginType = pluginTypes[plugName];
        break;
    }
}

// Handle bad plugin type
if (pluginType == null)
{
    Console.WriteLine($"Unknown plugin, please provide your desired plugin as a launch argument: ");
    Console.WriteLine("Available plugins: ");
    foreach (var plugName in pluginsAvailable)
    {
        Console.WriteLine($" - '{plugName}'");
    }
    return;
}

// Initialize the adapter's protocol and plugin type, then the API will initialize and boot the adapter.
adapter.SetProtocol(protocolManager);
adapter.SetPluginType(pluginType);
TebexApi.Instance.InitAdapter(adapter);

while (true)
{
    Console.Write("Tebex> ");
    var input = Console.ReadLine();

    if (string.IsNullOrEmpty(input))
    {
        continue;
    }
    
    if (input == "exit")
    {
        return;
    }

    if (input == "clear" || input == "cls")
    {
        Console.Clear();
        continue;
    }
    
    if (input.Contains("tebex."))
    {
        adapter.HandleTebexCommand(input);
        continue;
    }
    
    if (adapter.GetProtocol() != null) //Rcon passthrough
    {
        adapter.GetProtocol()?.Write(input); //missing "2" for auth
        var response = adapter.GetProtocol()?.Read();
        Console.WriteLine(response);
        continue;
    }
    
    // Null rcon
    Console.WriteLine("Unrecognized command. If you ran a server command, we are not connected to a game server.");
    Console.WriteLine("Please check your secret key and server connection.");
}