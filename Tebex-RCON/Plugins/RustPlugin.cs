using Tebex.Adapters;
using Tebex.API;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class RustPlugin : RconPlugin
    {
        public RustPlugin(TebexRconAdapter adapter) : base(adapter) {}

        public override string GetPluginVersion()
        {
            return "1.0.0";
        }
        
        public override bool IsPlayerOnline(TebexApi.DuePlayer player)
        {
            bool found = false;
            var cmdExecMessage = _rcon.Send("list");

            int tries = 0;
            while (tries < 10)
            {
                Thread.Sleep(200); // wait for websocket response to be polled and added to responses
                var message = _rcon.ReceiveResponseTo(cmdExecMessage.Id, 10);
                if (!message.Item2.Equals("")) // no response yet, error is present
                {
                    tries++;
                    continue;
                }

                // successfully got response to our list message
                return message.Item1.Response.Message.Contains(player.Name) || message.Item1.Response.Message.Contains(player.UUID);
            }

            return false;
        }

        public override RconConnection CreateRconConnection(string host, int port, string password)
        {
            return new WebsocketRcon(_adapter, host, port, password);
        }
    }   
}