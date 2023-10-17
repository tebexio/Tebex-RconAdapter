using Tebex.API;
using Tebex.RCON;

namespace Tebex.Plugins
{
    public class ConanExilesPlugin : TebexRconPlugin
    {
        // For game type auth
        private int _blueprintConfigVersion;
        private int _configVersion;

        private List<ConanPlayerInfo> lastPlayerList = new List<ConanPlayerInfo>();
        
        public ConanExilesPlugin(TebexRconClient client, TebexRconAdapter adapter) : base(client, adapter)
        {
            TebexRconAdapter.ExecuteEvery(TimeSpan.FromSeconds(120), () =>
            {
                GetOnlinePlayers();
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
            
            public static List<ConanPlayerInfo> ParsePlayerList(string input)
            {
                var playerInfoList = new List<ConanPlayerInfo>();
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

        public override string GetGameName()
        {
            return "Conan Exiles";
        }
        
        public void GetOnlinePlayers()
        {
            _adapter.LogDebug($"Querying server for online player list...");
            var currentPlayerList = ConanPlayerInfo.ParsePlayerList(_rcon.SendCommandAndReadResponse(2, "listplayers"));
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

        public override void ReplyPlayer(string playerId, string message)
        {
            throw new NotImplementedException();
        }

        public override bool IsPlayerOnline(string playerId)
        {
            foreach (var player in lastPlayerList)
            {
                if (player.PlatformID.Equals(playerId) || player.CharName == playerId)
                {
                    return true;
                }
            }

            return false;
        }
        
        public override bool AuthenticateGame(string gameType)
        {
            return true;
            
            /*
            try
            {
                _blueprintConfigVersion = Convert.ToInt32(_rcon.SendCommandAndReadResponse(2, "GetServerSetting BlueprintConfigVersion")
                    .Split('=')[1].Trim());
                _configVersion = Convert.ToInt32(_rcon.SendCommandAndReadResponse(2, "GetServerSetting ConfigVersion")
                    .Split('=')[1].Trim());

                if (_blueprintConfigVersion != 0 && _configVersion != 0)
                {
                    //TODO remote game auth
                    return _blueprintConfigVersion == 25 && _configVersion == 11;
                }
            }
            catch (Exception e)
            {
                _adapter.LogError("An error occurred while authenticating the game server: ");
                _adapter.LogError(e.Message);
            }

            return false;
            */
        }

        public override void HandleRconOutput(string message)
        {
            _adapter.LogDebug($"'{message}' <- RCON");
        }

        public override object GetPlayerRef(string idOrUsername)
        {
            return GetPlayerPositionId(idOrUsername);
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
        public int GetPlayerPositionId(string idOrUsername)
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
    }   
}