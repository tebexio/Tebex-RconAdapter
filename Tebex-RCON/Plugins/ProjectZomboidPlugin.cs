using Tebex.Adapters;
using Tebex.API;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class ProjectZomboidPlugin : TebexRconPlugin
    {
        public ProjectZomboidPlugin(ProtocolManagerBase protocolManager, BaseTebexAdapter adapter) : base(protocolManager, adapter)
        {
            new Thread(() =>
            {
                _protocolManager.PollRconMessages();    
            }).Start();
        }
        
        public override string GetGameName()
        {
            return "Project Zomboid";
        }
        
        public override void ReplyPlayer(string playerId, string message)
        {
            throw new NotImplementedException();
        }

        public override bool IsPlayerOnline(string playerId) // playerId will be in-game username provided by the player
        {
            /*
            _protocolManager.EnablePolling = false;
            _protocolManager.Write("players");
            bool found = false;
            var cmdExecMessage = _protocolManager.Read();
            _adapter.LogDebug($"player online check result: {cmdExecMessage}");
            _protocolManager.EnablePolling = true;
            return found;
            */

            /* Project Zomboid allows players to set their own "username" or in-game name within the server.
                 This should be collected from the player using a custom variable,
                 which is filled by the API and delivered via the RCON adapter.*/
            return true;
        }
        
        public override bool AuthenticateGame(string gameType)
        {
            return true;
        }

        public override void HandleRconOutput(string message)
        {
            _adapter.LogInfo($"'{message}' <- RCON");
        }

        public override object GetPlayerRef(string idOrUsername, TebexApi.Command command)
        {
            // We are seeking the player's custom in-game name to determine if they are online
            _adapter.LogDebug("Trying to fill player reference, need custom username!");
            _adapter.LogDebug(command.CommandToRun);
            
            /*
             * At time of execution the var names are not known 
             */
            return idOrUsername;
        }

        public override string ExpandGameUsernameVariables(string cmd, object playerObj)
        {
            return cmd;
        }
    }   
}