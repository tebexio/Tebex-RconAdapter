using System.Net.Sockets;
using System.Text;

public class TebexTelnetClient : IDisposable
{
    private TcpClient _tcpClient;
    private NetworkStream _networkStream;
    private StreamReader _streamReader;
    private StreamWriter _streamWriter;

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public TebexTelnetClient()
    {
        _tcpClient = new TcpClient();
    }

    public async Task<bool> ConnectAsync(string host, int port, string password, bool attemptReconnect)
    {
        try
        {
            await _tcpClient.ConnectAsync(host, port);
            _networkStream = _tcpClient.GetStream();
            _streamReader = new StreamReader(_networkStream, Encoding.ASCII);
            _streamWriter = new StreamWriter(_networkStream, Encoding.ASCII) { AutoFlush = true };
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting: {ex.Message}");
            Dispose();
            return false;
        }
    }

    public async Task<string> ReceiveAsync()
    {
        try
        {
            return await _streamReader.ReadLineAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error receiving data: {ex.Message}");
            Dispose();
            return null;
        }
    }

    public async Task<bool> SendAsync(string data)
    {
        try
        {
            await _streamWriter.WriteLineAsync(data);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending data: {ex.Message}");
            Dispose();
            return false;
        }
    }

    public void Dispose()
    {
        _streamWriter?.Dispose();
        _streamReader?.Dispose();
        _networkStream?.Dispose();
        _tcpClient?.Close();
    }
}