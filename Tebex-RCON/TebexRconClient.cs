using System.Net.Sockets;
using System.Text;

namespace Tebex.RCON
{
    public class TebexRconClient
    {
        private TebexRconPlugin? listener;
        private TcpClient tcpClient;
        private NetworkStream stream;
        private readonly string password;
        private readonly string host;
        private readonly int port;
        private readonly bool reconnectOnFail;
        private Thread reconnectThread;
        
        public TebexRconClient(string host, int port, string password, bool reconnectOnFail)
        {
            this.host = host;
            this.port = port;
            this.password = password;
            this.reconnectOnFail = reconnectOnFail;
            if (reconnectOnFail)
            {
                reconnectThread = new Thread(ReconnectLoop);
                reconnectThread.Start();
            }
        }

        private void ReconnectLoop()
        {
            while (true)
            {
                if (tcpClient == null || !tcpClient.Connected)
                {
                    var (success, exception) = Connect();
                    if (!success && exception != null)
                    {
                        Console.WriteLine($"Failed to connect to RCON: '{exception.Message}'. Trying again in 5 seconds...");
                    }
                }
                Thread.Sleep(5000); // wait 5 seconds before checking the connection again
            }
        }

        public string GetIpAndPort()
        {
            return $"{host}:{port}";
        }

        public void SetMessageListener(TebexRconPlugin plugin)
        {
            listener = plugin;
        }

        public (bool, Exception?) Connect()
        {
            try
            {
                tcpClient = new TcpClient(host, port);
                stream = tcpClient.GetStream();

                if (Authenticate())
                {
                    return (true, null);
                }

                return (false, new Exception("Could not authenticate game server type"));
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        private bool Authenticate()
        {
            return SendCommandAndReadResponse(3, password) == "Authenticated.";
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

            stream.Write(request, 0, request.Length);
        }

        public string SendCommandAndReadResponse(int requestType, string payload)
        {
            if (listener != null)
            {
                listener.GetAdapter().LogDebug($"RCON ({host}:{port}) -> {requestType}|'{payload}'");    
            }
            
            SendCommand(requestType, payload);

            byte[] response = new byte[4096];
            int bytesRead = stream.Read(response, 0, response.Length);

            int responseId = BitConverter.ToInt32(response, 4);
            int responseType = BitConverter.ToInt32(response, 8);
            string responseString = Encoding.UTF8.GetString(response, 12, bytesRead - 14);
            if (listener != null)
            {
                listener.GetAdapter().LogDebug($"'{responseString}' <- ({host}:{port}) RCON");    
            }
            
            
            return responseString;
        }

        public void ReadRconMessages()
        {
            while (true)
            {
                byte[] response = new byte[4096];
                int bytesRead = stream.Read(response, 0, response.Length);

                int responseId = BitConverter.ToInt32(response, 4);
                int responseType = BitConverter.ToInt32(response, 8);
                string responseString = Encoding.UTF8.GetString(response, 12, bytesRead - 14);

                listener.HandleRconOutput(responseString);
            }
        }

        public void Disconnect()
        {
            stream?.Close();
            tcpClient?.Close();
        }
    }
}