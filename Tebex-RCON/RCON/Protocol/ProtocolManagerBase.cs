using System.Net.Sockets;

namespace Tebex.RCON.Protocol;

public abstract class ProtocolManagerBase
{
    /** Set to true in order to constantly poll the connection for data */
    public bool EnablePolling = false;
    
    protected TcpClient? TcpClient;
    protected NetworkStream? Stream;
    protected string Password = "";
    protected string Host = "127.0.0.1";
    protected int Port = 25565;
    protected bool ReconnectOnFail = false;
    
    private Thread? _reconnectThread = null;
    protected TebexRconPlugin? Listener;

    public abstract string GetProtocolName();

    public abstract void Write(string data);
    public abstract string? Read();
    public abstract bool Connect(string host, int port, string password, bool attemptReconnect);
    public abstract void Close();
    
    public void StartReconnectThread()
    {
        if (ReconnectOnFail)
        {
            _reconnectThread = new Thread(ReconnectLoop);
            _reconnectThread.Start();
        }
    }
    
    public string GetIpAndPort()
    {
        return $"{Host}:{Port}";
    }
    
    protected void ReconnectLoop()
    {
        while (true)
        {
            if (TcpClient == null || !TcpClient.Connected)
            {
                try
                {
                    var success = Connect(Host, Port, Password, ReconnectOnFail);
                    if (!success)
                    {
                        Console.WriteLine($"RCON connection failed. Trying again in 5 seconds...");
                    }

                    Console.WriteLine("Reconnect succeeded.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception while connecting to RCON: '{ex.Message}'. Trying again in 5 seconds...");
                }
            }
            Thread.Sleep(5000); // wait 5 seconds before checking the connection again
        }
    }

    /**
     * Constantly reads a response from the remote connection and passes it to the listener's RCON output function
     * for handling.
     *
     * If you need to stop polling in order to send sequential messages to the server, set EnablePolling = false and
     * then run your subsequent Read() and Write(). When done, set EnablePolling = true 
     */
    public void PollRconMessages()
    {
        while (true)
        {
            if (TcpClient != null && TcpClient.Connected && EnablePolling)
            {
                var responseString = Read();
                if (responseString != null)
                {
                    Listener?.HandleRconOutput(responseString);    
                }
            }
            
            Thread.Sleep(2);
        }
    }
    
    public void SetMessageListener(TebexRconPlugin plugin)
    {
        Listener = plugin;
    }
}