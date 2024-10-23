using Tebex.Adapters;
using Tebex.API;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class ConanExilesPlugin : RconPlugin
    {
        // For game type auth
        private int _blueprintConfigVersion;
        private int _configVersion;

        private List<ConanPlayerInfo> lastPlayerList = new List<ConanPlayerInfo>();
        
        public ConanExilesPlugin(TebexRconAdapter adapter) : base(adapter)
        {
            TebexRconAdapter.ExecuteEvery(TimeSpan.FromSeconds(45), () =>
            {
                try
                {
                    GetOnlinePlayers();
                }
                catch (Exception e)
                {
                    _adapter.LogError($"Error while getting online players: {e.Message}");
                }
            });
        }

        public class ConanPlayerInfo
        {
            public int Idx { get; set; }
            public string CharName { get; set; }
            public string PlayerName { get; set; }
            public string UserID { get; set; }
            public string PlatformID { get; set; }
            public string PlatformName { get; set; }
            
            public static List<ConanPlayerInfo> ParsePlayerList(string? input)
            {
                var playerInfoList = new List<ConanPlayerInfo>();
                if (input == null)
                {
                    return playerInfoList;
                }
                
                string[] lines = input.Trim().Split('\n');
        
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] data = lines[i].Split('|');

                    playerInfoList.Add(new ConanPlayerInfo
                    {
                        Idx = int.Parse(data[0].Trim()),
                        CharName = data[1].Trim(),
                        PlayerName = data[2].Trim(),
                        UserID = data[3].Trim(),
                        PlatformID = data[4].Trim(),
                        PlatformName = data[5].Trim()
                    });
                }

                return playerInfoList;
            }
        }

        public void GetOnlinePlayers()
        {
            _adapter.LogDebug($"Querying server for online player list...");
            var listPacket = _rcon.Send("listplayers");
            var listResponse = _rcon.ReceiveNext();
            
            var currentPlayerList = ConanPlayerInfo.ParsePlayerList(listResponse.Message);
            _adapter.LogDebug($"Detected {currentPlayerList.Count} online Conan players");
            
            List<string> oldJoins = new List<string>();
            foreach (var playerInfo in lastPlayerList)
            {
                oldJoins.Add(playerInfo.PlatformID);
            }
            
            List<string> newJoins = new List<string>();
            foreach (var playerInfo in currentPlayerList)
            {
                if (!oldJoins.Contains(playerInfo.PlatformID))
                {
                    newJoins.Add(playerInfo.PlatformID);
                }
            }

            lastPlayerList = currentPlayerList;
            foreach (var id in newJoins)
            {
                //TODO Player IP is not accurate
                _adapter.OnUserConnected(id, "0.0.0.0");
            }
        }

        public override bool IsPlayerOnline(TebexApi.DuePlayer duePlayer)
        {
            foreach (var player in lastPlayerList)
            {
                if (player.PlatformID.Equals(duePlayer.UUID) || player.CharName == duePlayer.Name)
                {
                    return true;
                }
            }

            return false;
        }

        public override string GetPluginVersion()
        {
            return "1.0.0";
        }

        public override object GetPlayerRef(string idOrUsername)
        {
            return _getPlayerPositionId(idOrUsername);
        }

        public override string ExpandGameUsernameVariables(string cmd, object playerObj)
        {
            foreach (var playerInfo in lastPlayerList)
            {
                if (playerInfo.Idx == (int)playerObj) //playerObj is player position ID for Conan Exiles
                {
                    cmd = cmd.Replace("{playercharactername}", playerInfo.CharName);
                    break;
                }
            }

            return cmd;
        }

        /**
         * Conan Exiles identifies its players in commands via their positional ID in the players list. This
         * searches for the player in the players list and returns their "Idx" or their position in the list.
         */
        private int _getPlayerPositionId(string idOrUsername)
        {
            // Refreshes lastPlayerList
            GetOnlinePlayers();
            
            foreach (var playerInfo in lastPlayerList)
            {
                if (playerInfo.PlatformID.Equals(idOrUsername) || playerInfo.CharName.Equals(idOrUsername))
                {
                    return playerInfo.Idx;
                }
            }

            return -1;
        }

        public override bool HasCustomPlayerRef()
        {
            return true;
        }
    }   
}