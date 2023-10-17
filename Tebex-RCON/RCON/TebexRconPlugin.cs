using Tebex.RCON;

namespace Tebex.RCON
{
    public abstract class TebexRconPlugin
    {
        protected TebexRconClient _rcon;
        protected TebexRconAdapter _adapter;
        public TebexRconPlugin(TebexRconClient client, TebexRconAdapter adapter)
        {
            _adapter = adapter;
            _rcon = client;
            _rcon.SetMessageListener(this);
        }

        public string GetPluginVersion()
        {
            return "0.0.1";
        }
        
        public TebexRconAdapter GetAdapter()
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