using System;
using System.Net.Sockets;
using System.Text;
using Tebex.Adapters;

namespace Tebex.RCON.Protocol;

public class RconConnection
{
    protected TcpClient? client;
    protected NetworkStream? stream;
    protected Dictionary<int, RconPacket> requests;
    protected Dictionary<int, RconPacket> responses;
    protected TebexRconAdapter? _adapter;
    protected readonly string _host;
    protected readonly int _port;
    protected readonly string _password;

    private int _nextId = 1;
    
    public RconConnection(TebexRconAdapter adapter, string host, int port, string password)
    {
        _adapter = adapter;
        _host = host;
        _port = port;
        _password = password;
        requests = new Dictionary<int, RconPacket>();
    }

    public virtual Tuple<bool, string> Connect()
    {
        client = new TcpClient();
        try
        {
            client.Connect(_host, _port);
            stream = client.GetStream();
            return Auth();
        }
        catch (SocketException ex)
        {
            return new Tuple<bool, string>(false, $"Failed to connect to RCON server at {_host}:{_port}. {ex.Message}");
        }
    }

    protected virtual bool Reconnect()
    {
        CloseConnection();

        _adapter.LogError(_adapter.Error($"Connection to RCON server lost!"));
        int tries = 0;
        while (true)
        {
            try
            {
                tries++;
                _adapter.LogInfo(_adapter.Warn($"Attempting reconnect #{tries}..."));
                client = new TcpClient();
                client.Connect(_host, _port);
                stream = client.GetStream();
                _adapter.LogInfo(_adapter.Success("Reconnected."));
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

    protected virtual void CloseConnection()
    {
        stream?.Close();
        client?.Close();
    }

    public virtual Tuple<bool, string> Auth()
    {
        RconPacket authPacket = SendAuth(_password);
        RconPacket authResponse = ReceiveNext();
        if (authResponse.Id != authPacket.Id)
        {
            return new Tuple<bool, string>(false, $"Failed to login to RCON server at {_host}:{_port}. Invalid password.");
        }
        
        return new Tuple<bool, string>(true, "");
    }
    
    public int NextId()
    {
        return _nextId++;
    }

    public RconPacket Send(string message)
    {
        return SendPacket(RconPacket.Type.CommandRequest, message);
    }

    public RconPacket SendAuth(string password)
    {
        return SendPacket(RconPacket.Type.LoginRequest, password);
    }

    protected virtual RconPacket SendPacket(RconPacket.Type requestType, string message)
    {
        if (stream == null || client == null || !client.Connected)
        {
            if (!Reconnect())
            {
                throw new InvalidOperationException("Unable to reconnect to the server.");
            }
        }

        try
        {
            RconPacket packet = CreatePacket(requestType, message);
            byte[] request = SerializePacket(packet);
            stream.Write(request, 0, request.Length);
            requests.Add(packet.Id, packet);
            return packet;
        }
        catch (SocketException)
        {
            if (Reconnect())
            {
                return new RconPacket(-1, RconPacket.Type.CommandResponse, "reconnecting"); //dummy packet to exit reconnect logic
            }
            else
            {
                throw new InvalidOperationException("Failed to reconnect.");
            }
        }
    }

    private RconPacket CreatePacket(RconPacket.Type requestType, string message)
    {
        int id = NextId();
        return new RconPacket(id, requestType, message);
    }

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

    public RconPacket ReceiveNext()
    {
        try
        {
            return ReadPacket(10);
        }
        catch (Exception)
        {
            if (Reconnect())
            {
                // return a dummy packet so we can exit this func and carry on with normal operation after reconnect
                return new RconPacket(-1, RconPacket.Type.CommandResponse, "reconnecting");
            }
            else
            {
                throw new InvalidOperationException("Failed to reconnect.");
            }
        }
    }

    protected virtual RconPacket ReadPacket(int timeoutSeconds)
    {
        stream.ReadTimeout = timeoutSeconds * 1000;
        var response = new byte[4096];
        var bytesRead = stream.Read(response, 0, response.Length);
        if (bytesRead < 14)
        {
            throw new InvalidOperationException("Received invalid packet size.");
        }
        
        var responseId = BitConverter.ToInt32(response, 4);
        var responseType = BitConverter.ToInt32(response, 8);
        var responseString = Encoding.UTF8.GetString(response, 12, bytesRead - 14);
        
        var packet = new RconPacket(responseId, (RconPacket.Type)responseType, responseString);
        responses.Add(packet.Id, packet);
        return packet;
    }

    public virtual bool Polls()
    {
        return false;
    }
    
    public virtual Tuple<RconResponse, string> ReceiveResponseTo(int messageId, int retries)
    {
        if (!requests.ContainsKey(messageId))
        {
            return new Tuple<RconResponse, String>(new RconResponse(), "Message " + messageId + " has not been sent yet");
        }

        int tries = 0;
        while (tries < retries)
        {
            RconPacket request = requests[messageId];
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
