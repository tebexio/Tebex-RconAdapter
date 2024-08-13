using Tebex.Adapters;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class ArkPlugin : TebexRconPlugin
    {
        public ArkPlugin(ProtocolManagerBase protocolManager, BaseTebexAdapter adapter) : base(protocolManager, adapter)
        {
            new Thread(() =>
            {
                _protocolManager.PollRconMessages();    
            }).Start();
        }
        
        public override string GetGameName()
        {
            return "ARK: SE";
        }
        
        public override void ReplyPlayer(string playerId, string message)
        {
            throw new NotImplementedException();
        }

        public override bool IsPlayerOnline(string playerId)
        {
            _protocolManager.EnablePolling = false;
            
            _protocolManager.Write("listplayers");


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
            // expect "Please enter password"
            var message = _protocolManager.Read();
            if (message != null && message.Contains("enter password"))
            {
                if (TebexRconAdapter.PluginConfig.RconPassword == "")
                {
                    _adapter.LogInfo("This server requires a password. Please configure your password in tebex-config.json or run `tebex.setup`.");
                    return false;
                }
                
                // we will be prompted for a password first if enabled
                
                _protocolManager.Write(TebexRconAdapter.PluginConfig.RconPassword);
                
                // expect "Logon successful"
                message = _protocolManager.Read();
                if (!message.Contains("successful"))
                {
                    _adapter.LogInfo("The server did not accept our password. Please try again.");
                    return false;
                }
            }
            

            // Polling starts disabled for 7 Days until we successfully connect with the server.
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