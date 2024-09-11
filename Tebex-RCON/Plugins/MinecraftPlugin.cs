using Tebex.Adapters;
using Tebex.API;
using Tebex.RCON.Protocol;

namespace Tebex.Plugins
{
    public class MinecraftPlugin : RconPlugin
    {
        public MinecraftPlugin(TebexRconAdapter adapter) : base(adapter)
        {

        }

        public override string GetPluginVersion()
        {
            return "1.0.0";
        }

        public override bool IsPlayerOnline(TebexApi.DuePlayer player)
        {
            // We can allow the Minecraft server to tell us if the command succeeded or not by assuming the player is online.
            // Minecraft will return an error if the command fails which can be interpreted by the adapter.
            return true;
        }

        public override string ExpandGameUsernameVariables(string cmd, object playerObj)
        {
            return cmd;
        }
    }   
}