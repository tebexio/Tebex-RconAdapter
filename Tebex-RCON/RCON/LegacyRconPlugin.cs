using Tebex.Adapters;
using Tebex.RCON;
using Tebex.Triage;

namespace Tebex.RCON.Protocol
{
    /**
     * Implements game or protocol-specific actions using the RCON adapter
     */
    public abstract class LegacyRconPlugin
    {
        protected LegacyProtocolManager _protocol;
        protected BaseTebexAdapter _adapter;
        public LegacyRconPlugin(LegacyProtocolManager protocol, BaseTebexAdapter adapter)
        {
            _adapter = adapter;
            _protocol = protocol;
            _protocol.SetMessageListener(this);
        }

        public string GetPluginVersion()
        {
            return "1.1.0";
        }
        
        public BaseTebexAdapter GetAdapter()
        {
            return _adapter;
        }
        
        public TebexPlatform GetPlatform()
        {
            return new TebexPlatform(GetPluginVersion(), new TebexTelemetry("RCON-Adapter", GetPluginVersion(), _protocol.GetProtocolName()));
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