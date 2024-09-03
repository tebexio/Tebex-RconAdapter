using Tebex.Adapters;
using Tebex.Triage;

namespace Tebex.RCON.Protocol;

public abstract class RconPlugin
{
    protected TebexRconAdapter _adapter;
    protected RconConnection _connection;
    
    public RconPlugin(TebexRconAdapter adapter)
    {
        _adapter = adapter;
    }
    
    public TebexRconAdapter GetAdapter()
    {
        return _adapter;
    }
    
    public TebexPlatform GetPlatform()
    {
        return new TebexPlatform(GetPluginVersion(), new TebexTelemetry("RCON-Adapter", GetPluginVersion(), _connection.GetType().Name));
    }

    public virtual string ExpandGameUsernameVariables(string cmd, object playerObj)
    {
        return cmd;
    }
    
    public abstract bool IsPlayerOnline(string playerId);
    
    public abstract string GetPluginVersion();

    public virtual RconConnection CreateRconConnection(TebexRconAdapter adapter, string host, int port, string password)
    {
        if (_connection == null)
        {
            _connection = new RconConnection(adapter, host, port, password);   
        }
        return _connection;
    }
}