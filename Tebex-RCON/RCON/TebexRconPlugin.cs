using Tebex.Adapters;
using Tebex.RCON;

namespace Tebex.RCON.Protocol
{
    /**
     * Implements game or protocol-specific actions using the RCON adapter
     */
    public abstract class TebexRconPlugin
    {
        protected ProtocolManagerBase _protocolManager;
        protected BaseTebexAdapter _adapter;
        public TebexRconPlugin(ProtocolManagerBase protocolManager, BaseTebexAdapter adapter)
        {
            _adapter = adapter;
            _protocolManager = protocolManager;
            _protocolManager.SetMessageListener(this);
        }

        public string GetPluginVersion()
        {
            return "0.0.1";
        }
        
        public BaseTebexAdapter GetAdapter()
        {
            return _adapter;
        }
        
        public abstract void ReplyPlayer(string playerId, string player);

        public abstract bool IsPlayerOnline(string playerId);

        public abstract bool AuthenticateGame(string gameType);

        public abstract void HandleRconOutput(string message);

        public abstract string GetGameName();

        public abstract object GetPlayerRef(string idOrUsername);

        public abstract string ExpandGameUsernameVariables(string cmd, object playerObj);
    }
}