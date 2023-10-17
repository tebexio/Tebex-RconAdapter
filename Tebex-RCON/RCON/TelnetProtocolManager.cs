using System.Net.Sockets;
using System.Text;

namespace Tebex.RCON.Protocol
{
    public class TelnetProtocolManager : ProtocolManagerBase, IDisposable
    {
        private readonly TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private StreamReader _streamReader;
        private StreamWriter _streamWriter;

        public TelnetProtocolManager()
        {
            _tcpClient = new TcpClient();
        }

        public override bool Connect(string host, int port, string password, bool attemptReconnect)
        {
            try
            {

                Listener?.GetAdapter().LogDebug($" TELNET -> connect {host}:{port}");
                
                var connectTask = _tcpClient.ConnectAsync(host, port);
                var waitTask = connectTask.WaitAsync(TimeSpan.FromSeconds(5));
                waitTask.Wait();

                if (!connectTask.IsCompleted)
                {
                    throw new IOException("Timeout while connecting via telnet");
                }

                _networkStream = _tcpClient.GetStream();
                Listener?.GetAdapter().LogDebug($" TELNET -> stream opened");
                
                _streamReader = new StreamReader(_networkStream, Encoding.ASCII);
                _streamWriter = new StreamWriter(_networkStream, Encoding.ASCII) { AutoFlush = true };
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting: {ex.Message}");
                Close();
                return false;
            }
        }

        private string? _receive()
        {
            try
            {
                Listener?.GetAdapter().LogDebug($" TELNET -> read wait");
                var response = _streamReader.ReadLine();
                Listener?.GetAdapter().LogDebug($" TELNET <- response '{response}'");
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving data: {ex.Message}");
                Close();
                return null;
            }
        }

        private void _send(string data)
        {
            try
            {
                Listener?.GetAdapter().LogDebug($" TELNET -> write '{data}'");
                _streamWriter.WriteLine(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending data: {ex.Message}");
                Close();
            }
        }

        public void Dispose()
        {
            _streamWriter?.Dispose();
            _streamReader?.Dispose();
            _networkStream?.Dispose();
            _tcpClient?.Close();
        }

        public override string GetProtocolName()
        {
            return "telnet";
        }

        public override void Write(string data)
        {
            _send(data);
        }

        public override string? Read()
        {
            return _receive();
        }

        public override void Close()
        {
            Dispose();
        }
    }
}