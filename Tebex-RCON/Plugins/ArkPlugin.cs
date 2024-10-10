using Tebex.Adapters;
using Tebex.API;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class ArkPlugin : RconPlugin
    {
        private String _lastPlayerList;
        
        public ArkPlugin(TebexRconAdapter adapter) : base(adapter)
        {
            TebexRconAdapter.ExecuteEvery(TimeSpan.FromSeconds(5), () =>
            { 
                _adapter.LogDebug("listplayers");
                
                if (_adapter.GetRcon() == null)
                {
                    _adapter.LogDebug("no rcon");
                    return;
                }
                
                var listPlayersCommand = _adapter.GetRcon().Send("listplayers");
                RconPacket listPlayersResponse;
                listPlayersResponse = _adapter.GetRcon().ReceiveNext();
                
                // Keep Alive packets seem to knock things out of order, we can just ignore when we grab the wrong response
                // because we should be updating the list of players every few seconds anyway.
                if (listPlayersResponse != null && !listPlayersResponse.Message.Contains("But no response!!"))
                {
                    _adapter.LogDebug("received player list: " + listPlayersResponse.Message);
                    _lastPlayerList = listPlayersResponse.Message;
                }
            });
        }

        public override string GetPluginVersion()
        {
            return "1.0.1-DEV";
        }

        public override bool IsPlayerOnline(TebexApi.DuePlayer player)
        {
            return _lastPlayerList.Contains(player.UUID);
        }

        public override string ExpandGameUsernameVariables(string cmd, object playerObj)
        {
            return cmd;
        }
        
        public override RconConnection CreateRconConnection(string host, int port, string password)
        {
            return new RconConnection(_adapter, host, port, password);
        }
    }   
}