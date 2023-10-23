using System.Net.Sockets;
using System.Text;

namespace Tebex.RCON.Protocol
{
    public class TelnetProtocolManager : ProtocolManagerBase, IDisposable
    {
        private StreamReader _streamReader;
        private StreamWriter _streamWriter;

        public TelnetProtocolManager()
        {
            TcpClient = new TcpClient();
        }

        public override bool Connect(string host, int port, string password, bool attemptReconnect)
        {
            try
            {

                Listener?.GetAdapter().LogDebug($" TELNET -> connect {host}:{port}");
                
                var connectTask = TcpClient.ConnectAsync(host, port);
                var waitTask = connectTask.WaitAsync(TimeSpan.FromSeconds(5));
                waitTask.Wait();

                if (!connectTask.IsCompleted)
                {
                    throw new IOException("Timeout while connecting via telnet");
                }

                Stream = TcpClient.GetStream();
                Listener?.GetAdapter().LogDebug($" TELNET -> stream opened");
                
                _streamReader = new StreamReader(Stream, Encoding.ASCII);
                _streamWriter = new StreamWriter(Stream, Encoding.ASCII) { AutoFlush = true };
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
                return "";
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
            Stream?.Dispose();
            TcpClient?.Close();
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