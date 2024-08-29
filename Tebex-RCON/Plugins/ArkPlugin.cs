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

            ArkSeProtocolManager arkProtocol = (ArkSeProtocolManager)_protocolManager;
            RconResponse listPlayers = arkProtocol.SendCommandAndReadResponse(2, "listplayers");

            _adapter.LogDebug($"list player response: {listPlayers.Response.Message}");
            _adapter.LogDebug($"looking for player id '{playerId}'");
            
            bool found = listPlayers.Response.Message.Contains(playerId);
            _protocolManager.EnablePolling = true;
            
            return found;
        }
        
        public override bool AuthenticateGame(string gameType)
        {
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