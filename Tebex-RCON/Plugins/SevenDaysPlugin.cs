using Tebex_RCON;
using Tebex.API;
using Tebex.RCON;

namespace Tebex.Plugins
{
    public class SevenDaysPlugin : TebexTelnetPlugin
    {
        public SevenDaysPlugin(TebexTelnetClient client, TebexTelnetAdapter adapter) : base(client, adapter)
        {
            var receiveTask = client.ReceiveAsync();
            var sendTask = client.SendAsync("help");
            sendTask.Wait();

            Console.WriteLine("Waiting for response...");
            receiveTask.Wait(5000);

            if (receiveTask.IsCompleted)
            {
                Console.WriteLine("Finished!");
                Console.WriteLine(receiveTask.Result);
            }
            else
            {
                Console.WriteLine("Failed!");
            }
        }
        
        public override string GetGameName()
        {
            return "7 Days to Die";
        }
        
        public override void ReplyPlayer(string playerId, string message)
        {
            throw new NotImplementedException();
        }

        public override bool IsPlayerOnline(string playerId)
        {
            return false;
        }
        
        public override bool AuthenticateGame(string gameType)
        {
            return true;
        }

        public override void HandleRconOutput(string message)
        {
            _adapter.LogDebug($"'{message}' <- RCON");
        }

        public override object GetPlayerRef(string idOrUsername)
        {
            throw new NotImplementedException();
            return null;
        }
    }   
}