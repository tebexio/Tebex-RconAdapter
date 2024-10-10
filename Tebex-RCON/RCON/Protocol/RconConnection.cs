using System.Net.Sockets;
using System.Text;
using Tebex.Adapters;

namespace Tebex.RCON.Protocol;

/// <summary>
/// An RconConnection handles a standard implementation of the RCON protocol over TCP.
/// </summary>
public class RconConnection
{
    protected readonly string Host;
    protected readonly int Port;
    protected readonly string Password;
    protected TcpClient? Tcp;
    protected NetworkStream? Stream;
    protected readonly Dictionary<int, RconPacket> Requests;
    protected readonly Dictionary<int, RconPacket> Responses;
    protected readonly TebexRconAdapter Adapter;
    private int _nextId = 1;
    
    public RconConnection(TebexRconAdapter adapter, string host, int port, string password)
    {
        Adapter = adapter;
        Host = host;
        Port = port;
        Password = password;
        Requests = new Dictionary<int, RconPacket>();
        Responses = new Dictionary<int, RconPacket>();
    }

    /// <summary>
    /// Establishes a connection to the RCON server and reports if we are successful. The base implementation immediately
    /// attempts to run <see cref="Auth"/> after connection is established.
    /// </summary>
    /// <returns>On success (true, ""), otherwise (false, {errorMessage})</returns>
    public virtual Tuple<bool, string> Connect()
    {
        Tcp = new TcpClient();
        try
        {
            // 10 second timeout
            Tcp.ReceiveTimeout = 1000 * 10;
            Tcp.SendTimeout = 1000 * 10;
            
            Tcp.Connect(Host, Port);
            Stream = Tcp.GetStream();
            return Auth();
        }
        catch (SocketException ex)
        {
            return new Tuple<bool, string>(false, $"Failed to connect to RCON server at {Host}:{Port}. {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to re-establish connection to the RCON server every 5 seconds.
    /// </summary>
    /// <returns>True if reconnection was successful.</returns>
    protected virtual bool Reconnect()
    {
        CloseConnection();

        Adapter.LogError(Adapter.Error($"Connection to RCON server lost!"));
        int tries = 0;
        while (true)
        {
            try
            {
                tries++;
                Adapter.LogInfo(Adapter.Warn($"Attempting reconnect #{tries}..."));
                
                // Attempt reconnect and re-auth
                Tcp = new TcpClient();
                Tcp.Connect(Host, Port);
                Stream = Tcp.GetStream();
                var authSuccess = Auth();
                
                // Report whether we succeeded or failed.
                if (authSuccess.Item1)
                {
                    Adapter.LogInfo(Adapter.Success("Reconnected."));    
                }
                else
                {
                    // Failed authorization during a reconnect indicates the server's password changed. We don't want to keep our app running
                    // against a server that has changed its authorization.
                    Adapter.LogError(Adapter.Error("Authorization failed during reconnect. Did the RCON server password change?"));
                    Environment.Exit(1);
                }
                
                return true;
            }
            catch (SocketException e)
            {
                Thread.Sleep(5000);
                continue;
            }    
        }

        return false;
    }

    /// <summary>
    /// Closes and cleans up RCON connection info.
    /// </summary>
    protected virtual void CloseConnection()
    {
        Stream?.Close();
        Tcp?.Close();
    }

    /// <summary>
    /// Authorizes the connection to the RCON server.
    /// </summary>
    /// <returns>(true, "") if authorization succeeded. Otherwise (false, "{errorMessage}")</returns>
    public virtual Tuple<bool, string> Auth()
    {
        RconPacket authPacket = SendAuth(Password);
        RconPacket authResponse = ReceiveNext();

        // Conan Exiles responds with "Authenticated." when successful but doesn't follow the proper RCON protocol of 
        // responding with the same packet ID. Handle "Authenticated" here because everything else about the protocol is the same.
        if (authResponse.Message.Equals("Authenticated."))
        {
            return new Tuple<bool, string>(true, "");
        }
        
        // Successful authorization will respond with an identical packet ID
        if (authResponse.Id != authPacket.Id)
        {
            return new Tuple<bool, string>(false, $"Failed to login to RCON server at {Host}:{Port}. Invalid password.");
        }
        
        return new Tuple<bool, string>(true, "");
    }
    
    /// <summary>
    /// Increments the next ID available for an RCON packet
    /// </summary>
    /// <returns>The next ID available to use.</returns>
    public int NextId()
    {
        return _nextId++;
    }

    /// <summary>
    /// Sends an RCON command to the server. The request is returned as an RconPacket for later use if necessary.
    /// </summary>
    /// <param name="message">The command for the server to execute.</param>
    /// <returns>The request as a <see cref="RconPacket"/></returns>
    public RconPacket Send(string message)
    {
        return SendPacket(RconPacket.Type.CommandRequest, message);
    }

    /// <summary>
    /// Sends an authorization packet to the server with the given password.
    /// </summary>
    /// <param name="password">The RCON password.</param>
    /// <returns>The request as a <see cref="RconPacket"/></returns>
    public RconPacket SendAuth(string password)
    {
        return SendPacket(RconPacket.Type.LoginRequest, password);
    }
    
    /// <summary>
    /// Sends a packet of a given type through our connection to the RCON server.
    /// </summary>
    /// <param name="packetType">The type of packet to send.</param>
    /// <param name="message">The packet's message or payload.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected virtual RconPacket SendPacket(RconPacket.Type packetType, string message)
    {
        // Ensure that all connection parameters are assigned and still valid
        if (Stream == null || Tcp == null || !Tcp.Connected)
        {
            if (!Reconnect())
            {
                throw new InvalidOperationException("Unable to reconnect to the RCON server.");
            }
        }

        // Create and send the RCON packet through our connection stream
        try
        {
            RconPacket packet = new RconPacket(NextId(), packetType, message);
            byte[] request = SerializePacket(packet);
            Stream.Write(request, 0, request.Length);

            if (Requests.ContainsKey(packet.Id))
            {
                Requests.Remove(packet.Id);
            }
            
            Requests.Add(packet.Id, packet);
            return packet;
        }
        catch (SocketException e)
        {
            Adapter.LogError(e.Message);
            if (Reconnect())
            {
                //return a dummy packet to exit reconnect logic
                return new RconPacket(-1, RconPacket.Type.CommandResponse, "reconnecting after send fail");
            }
            else
            {
                throw new InvalidOperationException("Unable to reconnect to the RCON server.");
            }
        }
    }

    /// <summary>
    /// Writes an RCON packet to its expected byte format
    /// </summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <returns>Serialized RCON packet bytes</returns>
    private byte[] SerializePacket(RconPacket packet)
    {
        byte[] commandBytes = Encoding.UTF8.GetBytes(packet.Message);
        byte[] request = new byte[12 + commandBytes.Length + 2];
        BitConverter.GetBytes(request.Length - 4).CopyTo(request, 0);
        BitConverter.GetBytes(packet.Id).CopyTo(request, 4);
        BitConverter.GetBytes((int)packet.PacketType).CopyTo(request, 8);
        commandBytes.CopyTo(request, 12);
        return request;
    }

    /// <summary>
    /// ReceiveNext is a blocking operation that will wait for a maximum of 10 seconds for the RCON server to respond
    /// before considering it timed out.
    /// </summary>
    /// <returns>The next <see cref="RconPacket"/> received by the client.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public RconPacket ReceiveNext()
    {
        try
        {
            return ReadPacket(10);
        }
        catch (Exception e)
        {
            Adapter.LogError(e.Message);
            Console.WriteLine(e.StackTrace);
            if (Reconnect())
            {
                // return a dummy packet so we can exit this func and carry on with normal operation after reconnect
                return new RconPacket(-1, RconPacket.Type.CommandResponse, "reconnecting after receive fail");
            }
            else
            {
                throw new InvalidOperationException("Failed to reconnect to the RCON server.");
            }
        }
    }

    /// <summary>
    /// ReadPacket will read the next RCON packet from our connected stream.
    /// </summary>
    /// <param name="timeoutSeconds">The number of seconds to wait for data before considering timeout.</param>
    /// <returns>The next <see cref="RconPacket"/> received by the client.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected virtual RconPacket ReadPacket(int timeoutSeconds)
    {
        Stream.ReadTimeout = timeoutSeconds * 1000;
        
        var response = new byte[4096];
        var bytesRead = Stream.Read(response, 0, response.Length);
        if (bytesRead < 14)
        {
            throw new InvalidOperationException("Received invalid packet size.");
        }
        
        // Read the next RCON packet
        var responseId = BitConverter.ToInt32(response, 4);
        var responseType = BitConverter.ToInt32(response, 8);
        var responseString = Encoding.UTF8.GetString(response, 12, bytesRead - 14);
        
        // Create a new RconPacket based on the received data. We only add this packet to Responses if it is an actual
        // response to a command. Generally server logs and other data will use -1 as their packet ID.
        var packet = new RconPacket(responseId, (RconPacket.Type)responseType, responseString);
        if (packet.Id > 0)
        {
            if (Responses.ContainsKey(packet.Id))
            {
                Responses.Remove(packet.Id);
            }
            
            Responses.Add(packet.Id, packet);
        }
        return packet;
    }

    /// <summary>
    /// Indicates if we should expect the connection to continually poll from the server. Some servers send keepalives or their
    /// server logs when connected to RCON. This should be checked before attempting to use <see cref="ReceiveNext"/>
    /// because Polling connections will cause a timeout as the stream is always blocked for reading.
    /// Instead, use <see cref="ReceiveResponseTo"/> for polling connections. Non-polling connections can be safely read sequentially.
    /// </summary>
    /// <returns></returns>
    public virtual bool Polls()
    {
        return false;
    }
    
    /// <summary>
    /// Gets a response to a given message ID within a set number of retries.
    /// </summary>
    /// <param name="messageId">The message ID of the request</param>
    /// <param name="retries">Number of retries to attempt reading a response, for sequential connections.</param>
    /// <returns>The RconResponse pair for the original message ID, or a Tuple containing an error string as Item2</returns>
    public virtual Tuple<RconResponse, string> ReceiveResponseTo(int messageId, int retries)
    {
        if (!Requests.ContainsKey(messageId))
        {
            return new Tuple<RconResponse, String>(new RconResponse(), "Message " + messageId + " has not been sent yet");
        }

        int tries = 0;
        while (tries < retries)
        {
            RconPacket request = Requests[messageId];
            RconPacket next = ReceiveNext();
            if (next.Id == messageId)
            {
                return new Tuple<RconResponse, String>(new RconResponse(request, next), "");
            }
            tries++;
        }

        return new Tuple<RconResponse, string>(new RconResponse(), "Did not receive response to message " + messageId + " within retry limit");
    }
}
