using Tebex.Adapters;
using Tebex.API;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class ArkPlugin : RconPlugin
    {
        public ArkPlugin(TebexRconAdapter adapter) : base(adapter)
        {
        }

        public override string GetPluginVersion()
        {
            return "1.0.0";
        }

        public override bool IsPlayerOnline(TebexApi.DuePlayer player)
        {
            var listPlayersCommand = _rcon.Send("listplayers");
            var listPlayersResponse = _rcon.ReceiveResponseTo(listPlayersCommand.Id, 100);
            if (!listPlayersResponse.Item2.Equals("")) // error present
            {
                return false;
            }

            return  listPlayersResponse.Item1.Response.Message.Contains(player.UUID);
        }

        public override string ExpandGameUsernameVariables(string cmd, object playerObj)
        {
            return cmd;
        }
        
        public override RconConnection CreateRconConnection(string host, int port, string password)
        {
            return new TelnetRcon(_adapter, host, port, password);
        }
    }   
}