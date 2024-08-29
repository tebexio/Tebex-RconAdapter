using Tebex.Adapters;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class RustPlugin : TebexRconPlugin
    {
        public RustPlugin(ProtocolManagerBase protocolManager, BaseTebexAdapter adapter) : base(protocolManager, adapter)
        {
            // new Thread(() =>
            // {
            //     _protocolManager.PollRconMessages();    
            // }).Start();
        }
        
        public override string GetGameName()
        {
            return "Rust";
        }
        
        public override void ReplyPlayer(string playerId, string message)
        {
            throw new NotImplementedException();
        }

        public override bool IsPlayerOnline(string playerId)
        {
            _protocolManager.EnablePolling = false;
            
            _protocolManager.Write("quit");


            bool found = false;
            var cmdExecMessage = _protocolManager.Read();

            while (true)
            {
                // After command exec message, this will be the first connected player
                var message = _protocolManager.Read();

                //TODO possible conflict with other commands that might be ran at the same time?
                if (message.Contains("pltfmid=") && message.Contains(playerId))
                {
                    found = true;
                    break;
                }

                if (message.Contains("Total of")) // End of player list is a total of the connected players
                {
                    break;
                }
            }
            
            _protocolManager.EnablePolling = true;
            return found;
        }
        
        public override bool AuthenticateGame(string gameType)
        {
            _protocolManager.EnablePolling = false;
            
            WebsocketProtocolManager protocol = (WebsocketProtocolManager)_protocolManager;

            protocol.Write("quit");
            _protocolManager.EnablePolling = true;
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