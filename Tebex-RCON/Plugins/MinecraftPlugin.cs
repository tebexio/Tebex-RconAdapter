using Tebex.Adapters;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class MinecraftPlugin : TebexRconPlugin
    {
        public MinecraftPlugin(ProtocolManagerBase protocolManager, BaseTebexAdapter adapter) : base(protocolManager, adapter)
        {
            new Thread(() =>
            {
                _protocolManager.PollRconMessages();    
            }).Start();
        }
        
        public override string GetGameName()
        {
            return "Minecraft";
        }
        
        public override void ReplyPlayer(string playerId, string message)
        {
            throw new NotImplementedException();
        }

        public override bool IsPlayerOnline(string playerId)
        {
            // allows the Minecraft server to tell us if the command succeeded or not by assuming the player is online
            return true;
        }
        
        public override bool AuthenticateGame(string gameType)
        {
            //_protocolManager.Write(TebexRconAdapter.PluginConfig.RconPassword);
            //_protocolManager.EnablePolling = true;
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