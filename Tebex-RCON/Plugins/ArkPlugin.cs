using Tebex.Adapters;
using Tebex.API;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class ArkPlugin : RconPlugin
    {
        private String _lastPlayerList = "";
        
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

                var rcon = _adapter.GetRcon();
                if (!rcon.IsConnected())
                {
                    return;
                }
                
                // Hold the connection lock so the command queue can't consume our response (or we consume theirs).
                Tuple<RconResponse, string> result;
                lock (rcon.SyncRoot)
                {
                    var listPlayersCommand = rcon.Send("listplayers");
                    if (listPlayersCommand.Id <= 0)
                    {
                        return; // dummy packet, connection was just re-established
                    }
                    
                    // Match on packet ID so stray keep-alive/old packets can't be mistaken for the player list
                    result = rcon.ReceiveResponseTo(listPlayersCommand.Id, 3);
                }
                
                if (result.Item2 != "")
                {
                    _adapter.LogDebug("failed to get player list: " + result.Item2);
                    return;
                }
                
                var message = result.Item1.Response.Message;
                if (message.Contains("But no response!!"))
                {
                    return;
                }
                
                _adapter.LogDebug("received player list: " + message);
                _lastPlayerList = message;
            });
        }

        public override string GetPluginVersion()
        {
            return "1.0.2";
        }

        public override bool IsPlayerOnline(TebexApi.DuePlayer player)
        {
            if (string.IsNullOrEmpty(_lastPlayerList))
            {
                _adapter.LogDebug("player list not yet received, assuming " + player.Name + " is offline");
                return false;
            }
            
            bool foundUuid = _lastPlayerList.Contains(player.UUID);
            if (!foundUuid)
            {
                _adapter.LogDebug("did not find " + player.Name + " by uuid in player list: " + player.UUID);
                bool foundName = _lastPlayerList.Contains(player.Name);
                if (!foundName)
                {
                    _adapter.LogDebug("did not find " + player.Name + " by name in player list");
                    return false;
                }
                _adapter.LogDebug("successfully found " + player.Name + " by name in player list");
                return true;
            }
            return foundUuid;
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