using Tebex.Adapters;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class ProjectZomboidPlugin : TebexRconPlugin
    {
        public ProjectZomboidPlugin(ProtocolManagerBase protocolManager, BaseTebexAdapter adapter) : base(protocolManager, adapter)
        {
        }
        
        public override string GetGameName()
        {
            return "Project Zomboid";
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
            _adapter.LogInfo($"'{message}' <- RCON");
        }

        public override object GetPlayerRef(string idOrUsername)
        {
            // Ref will be the desired steamid
            return idOrUsername;
        }

        public override string ExpandGameUsernameVariables(string cmd, object playerObj)
        {
            return cmd;
        }
    }   
}