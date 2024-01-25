using System.Net;
using System.Net.Sockets;
using System.Text;
using BattleNET;

namespace Tebex.RCON.Protocol
{
    public class BattleNetProtocolManager : ProtocolManagerBase
    {
        protected BattlEyeClient? BEClient;
        public override bool Connect(string host, int port, string password, bool reconnectOnFail)
        {
            Host = host;
            Port = port;
            Password = password;
            ReconnectOnFail = reconnectOnFail;

            var loginCredentials = new BattlEyeLoginCredentials(IPAddress.Parse(host), port, password);

            BattlEyeClient b = new BattlEyeClient(loginCredentials);
            b.BattlEyeMessageReceived += BEMessageReceived;
            b.BattlEyeConnected += BEConnected;
            b.BattlEyeDisconnected += BEDisconnected;
            b.ReconnectOnPacketLoss = reconnectOnFail;
            var result = b.Connect();
            
            Console.WriteLine($"Connect result: {result}");
            return result == BattlEyeConnectionResult.Success;
        }

        private void BEMessageReceived(BattlEyeMessageEventArgs args)
        {
            Listener.GetAdapter().LogDebug($"BEMessage {args.Message}");
        }
        
        private void BEConnected(BattlEyeConnectEventArgs args)
        {
            Listener.GetAdapter().LogDebug($"BEConnected {args.Message}");
        }
        
        private void BEDisconnected(BattlEyeDisconnectEventArgs args)
        {
            Listener.GetAdapter().LogDebug($"BEDisconnected {args.Message}");
        }

        private bool Authenticate()
        {
            return SendCommandAndReadResponse(3, Password) == "Authenticated.";
        }

        public void SendCommand(int requestType, string command)
        {
            int sent = BEClient.SendCommand(BattlEyeCommand.RConPassword, command);
            
            if (Listener != null)
            {
                Listener.GetAdapter().LogDebug($"RCON ({Host}:{Port}) -> {requestType}|'{sent} bytes'");    
            }
        }

        public string SendCommandAndReadResponse(int requestType, string payload)
        {
            SendCommand(requestType, payload);
            return "";
        }

        public void Disconnect()
        {
            BEClient?.Disconnect();
        }

        public override string GetProtocolName()
        {
            return "battleye";
        }

        public override void Write(string data)
        {
            BEClient?.SendCommand(data);
        }

        public override string? Read()
        {
            return "";
        }
        
        public override void Close()
        {
            throw new NotImplementedException();
        }
    }
}