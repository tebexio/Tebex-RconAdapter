using Tebex.Adapters;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class SevenDaysPlugin : TebexRconPlugin
    {
        public SevenDaysPlugin(ProtocolManagerBase protocolManager, BaseTebexAdapter adapter) : base(protocolManager, adapter)
        {
            new Thread(() =>
            {
                _protocolManager.PollRconMessages();    
            }).Start();
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
            throw new NotImplementedException();
            return null;
        }

        public override string ExpandGameUsernameVariables(string cmd, object playerObj)
        {
            throw new NotImplementedException();
        }
    }   
}