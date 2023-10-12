using Tebex_RCON;
using Tebex.RCON;

namespace Tebex.RCON
{
    public abstract class TebexTelnetPlugin
    {
        protected TebexTelnetClient _telnet;
        protected TebexTelnetAdapter _adapter;
        
        public TebexTelnetPlugin(TebexTelnetClient client, TebexTelnetAdapter adapter)
        {
            _adapter = adapter;
            _telnet = client;
        }
        
        public string GetPluginVersion()
        {
            return "0.0.1";
        }
        
        public TebexTelnetAdapter GetAdapter()
        {
            return _adapter;
        }
        
        public TebexTelnetClient GetTelnetClient()
        {
            return _telnet;
        }
        
        public abstract void ReplyPlayer(string playerId, string player);

        public abstract bool IsPlayerOnline(string playerId);

        public abstract bool AuthenticateGame(string gameType);

        public abstract void HandleRconOutput(string message);

        public abstract string GetGameName();

        public abstract object GetPlayerRef(string idOrUsername);
    }
}