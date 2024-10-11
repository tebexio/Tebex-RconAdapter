using Tebex.Adapters;
using Tebex.API;
using Tebex.Triage;

namespace Tebex.RCON.Protocol;

/// <summary>
/// An RCON plugin defines logic for enhanced RCON functions such as command success, player online checking, and server events.
/// </summary>
public abstract class RconPlugin
{
    /// <summary>
    /// Reference to the internal RconAdapter available to this plugin.
    /// </summary>
    protected TebexRconAdapter _adapter;
    
    /// <summary>
    /// Reference to the internal RconConnection available to this plugin.
    /// </summary>
    protected RconConnection _rcon;
    
    /// <summary>
    /// Creates a new RconPlugin instance.
    /// </summary>
    /// <param name="adapter">The RconAdapter that is booting this plugin.</param>
    public RconPlugin(TebexRconAdapter adapter)
    {
        _adapter = adapter;
    }
    
    /// <summary>
    /// Defines information about the current platform / environment we are executing in.
    /// </summary>
    /// <returns><see cref="TebexPlatform"/></returns>
    public TebexPlatform GetPlatform()
    {
        if (_rcon != null)
        {
            return new TebexPlatform(GetPluginVersion(), new TebexTelemetry("RCON-Adapter", TebexRconAdapter.Version, _rcon.GetType().Name));    
        }
        else
        {
            return new TebexPlatform(GetPluginVersion(), new TebexTelemetry("RCON-Adapter", TebexRconAdapter.Version, TebexRconAdapter.Version));
        }
    }

    /// <summary>
    /// Expands game-specific username variables, returning the parsed command.
    /// </summary>
    /// <param name="cmd">The command containing username tags to parse ex. {playercharname} </param>
    /// <param name="playerObj">Player reference usually provided by GetPlayerRef()</param>
    /// <returns>Parsed command string with any supported game username variables replaced.</returns>
    public virtual string ExpandGameUsernameVariables(string cmd, object playerObj)
    {
        return cmd;
    }
    
    /// <summary>
    /// Determines via RCON commands if a given DuePlayer is currently online.
    /// </summary>
    /// <param name="duePlayer">The <see cref="TebexApi.DuePlayer"/> containing name for online status lookup.</param>
    /// <returns>True if the player is online on the server and can accept commands.</returns>
    public abstract bool IsPlayerOnline(TebexApi.DuePlayer duePlayer);
    
    public abstract string GetPluginVersion();

    /// <summary>
    /// CreateRconConnection is overridden by any plugins that use a non-standard RCON protocol to create their own
    /// alternative RconConnection type.
    /// </summary>
    /// <param name="host">The RCON host/IP</param>
    /// <param name="port">The RCON port</param>
    /// <param name="password">The RCON password</param>
    /// <returns>An instance of the appropriate <see cref="RconConnection"/> for our plugin.</returns>
    public virtual RconConnection CreateRconConnection(string host, int port, string password)
    {
        if (_rcon == null)
        {
            _rcon = new RconConnection(_adapter, host, port, password);   
        }
        return _rcon;
    }

    /// <summary>
    /// Signals if a plugin has custom handling for player references/player IDs.
    /// </summary>
    /// <returns>True if the plugin implements changes to a default player reference.</returns>
    public virtual bool HasCustomPlayerRef()
    {
        return false;
    }
    
    /// <summary>
    /// See <see cref="BaseTebexAdapter.GetPlayerRef"/>
    /// </summary>
    /// <param name="playerId"></param>
    /// <returns>Empty object causing bypass of player ref check</returns>
    public virtual object GetPlayerRef(string playerId)
    {
        return new Object(); // to bypass ref check in BaseTebexAdapter without rcon plugin
    }
}