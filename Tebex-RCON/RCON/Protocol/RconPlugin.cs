using Tebex.Adapters;
using Tebex.Triage;

namespace Tebex.RCON.Protocol;

public abstract class RconPlugin
{
    protected TebexRconAdapter _adapter;
    protected RconConnection _rcon;
    
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
        return new TebexPlatform(GetPluginVersion(), new TebexTelemetry("RCON-Adapter", GetPluginVersion(), _rcon.GetType().Name));
    }

    public virtual string ExpandGameUsernameVariables(string cmd, object playerObj)
    {
        return cmd;
    }
    
    public abstract bool IsPlayerOnline(string playerId);
    
    public abstract string GetPluginVersion();

    public virtual RconConnection CreateRconConnection(TebexRconAdapter adapter, string host, int port, string password)
    {
        if (_rcon == null)
        {
            _rcon = new RconConnection(adapter, host, port, password);   
        }
        return _rcon;
    }

    public virtual bool HasCustomPlayerRef()
    {
        return false;
    }
    
    public virtual object GetPlayerRef(string playerId)
    {
        return new Object(); // to bypass ref check in BaseTebexAdapter without rcon plugin
    }
}