using System.Net.Sockets;
using System.Text;

namespace Tebex.RCON.Protocol
{
    public class BaseProtocolManager : LegacyProtocolManager
    {
        public override bool Connect(string host, int port, string password, bool reconnectOnFail)
        {
            Host = host;
            Port = port;
            Password = password;
            ReconnectOnFail = reconnectOnFail;
            TcpClient = new TcpClient(host, port);
            Stream = TcpClient.GetStream();

            if (Authenticate())
            {
                return true;
            }

            return false;
        }

        private bool Authenticate()
        {
            return SendCommandAndReadResponse(3, Password) == "Authenticated.";
        }

        public void SendCommand(int requestType, string command)
        {
            int requestId = new Random().Next(1, int.MaxValue);
            byte[] commandBytes = Encoding.UTF8.GetBytes(command);
            byte[] request = new byte[14 + commandBytes.Length];

            // Length
            Array.Copy(BitConverter.GetBytes(10 + commandBytes.Length), request, 4);

            // Request ID
            Array.Copy(BitConverter.GetBytes(requestId), 0, request, 4, 4);

            // Type
            Array.Copy(BitConverter.GetBytes(requestType), 0, request, 8, 4);

            // Actual Command
            Array.Copy(commandBytes, 0, request, 12, commandBytes.Length);

            // Null terminator
            request[12 + commandBytes.Length] = 0x00;
            request[13 + commandBytes.Length] = 0x00;

            if (Listener != null)
            {
                Listener.GetAdapter().LogDebug($"RCON ({Host}:{Port}) -> id: {requestId} | type:{requestType}| length: {request.Length} bytes | body: '{command}'");    
            }
            Stream.Write(request, 0, request.Length);
        }

        public string SendCommandAndReadResponse(int requestType, string payload)
        {
            SendCommand(requestType, payload);

            byte[] response = new byte[4096];
            if (Listener != null)
            {
                Listener.GetAdapter().LogDebug($"RCON ({Host}:{Port}) -> READ WAIT'");
            }

            int bytesRead = Stream.Read(response, 0, response.Length);

            int responseId = BitConverter.ToInt32(response, 4);
            int responseType = BitConverter.ToInt32(response, 8);
            string responseString = Encoding.UTF8.GetString(response, 12, bytesRead - 14);

            if (Listener != null)
            {
                Listener.GetAdapter().LogDebug($"RCON ({Host}:{Port}) <- {responseString} | length: {bytesRead} | responseId: {responseId} | responseType: {responseType} | responseString: {responseString}'");
            }
            return responseString;
        }

        public void Disconnect()
        {
            Stream?.Close();
            TcpClient?.Close();
        }

        public override string GetProtocolName()
        {
            return "RCON";
        }

        public override void Write(string data)
        {
            SendCommand(2, data);
        }

        public override string? Read()
        {
            byte[] response = new byte[4096];
            int bytesRead = Stream.Read(response, 0, response.Length);

            int responseId = BitConverter.ToInt32(response, 4);
            int responseType = BitConverter.ToInt32(response, 8);
            string responseString = Encoding.UTF8.GetString(response, 12, bytesRead - 14);

            if (Listener != null)
            {
                Listener.GetAdapter().LogDebug($" RCON ({Host}:{Port}) <- {responseString}'");
            }
            return responseString;
        }

        public override void Close()
        {
            throw new NotImplementedException();
        }
    }
}