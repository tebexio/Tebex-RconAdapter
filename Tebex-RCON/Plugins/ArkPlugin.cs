using Tebex.Adapters;
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

        public override bool IsPlayerOnline(string playerId)
        {
            var listPlayersCommand = _rcon.Send("listplayers");
            var listPlayersResponse = _rcon.ReceiveResponseTo(listPlayersCommand.Id, 100);
            if (!listPlayersResponse.Item2.Equals("")) // error present
            {
                return false;
            }

            return  listPlayersResponse.Item1.Response.Message.Contains(playerId);
        }

        public override string ExpandGameUsernameVariables(string cmd, object playerObj)
        {
            return cmd;
        }
        
        public override RconConnection CreateRconConnection(TebexRconAdapter adapter, string host, int port, string password)
        {
            return new TelnetRcon(adapter, host, port, password);
        }
    }   
}