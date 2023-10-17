using Tebex.Adapters;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class SevenDaysPlugin : TebexRconPlugin
    {
        public SevenDaysPlugin(ProtocolManagerBase protocolManager, BaseTebexAdapter adapter) : base(protocolManager, adapter)
        {
        }
        
        public override string GetGameName()
        {
            return "7 Days to Die";
        }
        
        public override void ReplyPlayer(string playerId, string message)
        {
            throw new NotImplementedException();
        }

        public override bool IsPlayerOnline(string playerId)
        {
            return false;
        }
        
        public override bool AuthenticateGame(string gameType)
        {
            return true;
        }

        public override void HandleRconOutput(string message)
        {
            _adapter.LogDebug($"'{message}' <- RCON");
        }

        public override object GetPlayerRef(string idOrUsername)
        {
            throw new NotImplementedException();
            return null;
        }

        public override string ExpandGameUsernameVariables(string cmd, object playerObj)
        {
            throw new NotImplementedException();
        }
    }   
}