using System.Net.Sockets;
using System.Text;
using Tebex.Adapters;

namespace Tebex.RCON.Protocol;

/// <summary>
/// TelnetRcon is an implementation of Rcon through a Telnet connection.
/// </summary>
public class TelnetRcon : RconConnection
{
    private StreamReader _streamReader;
    private StreamWriter _streamWriter;
    private bool _stop = false;
    
    public TelnetRcon(TebexRconAdapter adapter, string host, int port, string password) : base(adapter, host, port, password)
    {
    }

    public override Tuple<bool, string> Connect()
    {
        var success = false;
        var error = "";
        Tcp = new TcpClient();
        try
        {
            Tcp.Connect(Host, Port);
            Stream = Tcp.GetStream();
            success = true;
        }
        catch (Exception e)
        {
            error = e.Message;
        }

        if (success) // successful TCP connection
        {
            _streamReader = new StreamReader(Stream, Encoding.ASCII);
            _streamWriter = new StreamWriter(Stream, Encoding.ASCII) { AutoFlush = true };
            
            // Authorize with the telnet server after setting up the appropriate streams
            var authResult = Auth();
            if (authResult.Item1) // successful auth
            {
                // Start polling thread
                new Thread(() =>
                {
                    try
                    {
                        while (!_stop)
                        {
                            var packet = ReadPacket(-1);
                            Adapter.LogInfo(packet.ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        //pass if the poller fails to read a packet, usually during reconnect
                    }
                }).Start();
                
            }
            return new Tuple<bool, string>(success, "");
        }

        return new Tuple<bool, string>(false, error);
    }

    public override Tuple<bool, string> Auth()
    {
        // We can expect "Please enter password" from 7 Days to Die, unsure about any other RCON telnet connections.
        var message = ReceiveNext().Message;
        if (message.Contains("enter password"))
        {
            if (TebexRconAdapter.PluginConfig.RconPassword == "")
            {
                return new Tuple<bool, string>(false, "This server requires a password. Please configure your password in tebex-config.json or run `tebex.setup`");
            }
            
            Send(Password);
                
            // expect "Logon successful" from 7 Days to Die
            var passwordResponse = ReceiveNext();
            if (!passwordResponse.Message.Contains("successful"))
            {
                return new Tuple<bool, string>(false, "The server did not accept our password. Please try again.");
            }
        }

        return new Tuple<bool, string>(true, "");
    }

    protected override RconPacket SendPacket(RconPacket.Type packetType, string message)
    {
        var packet = new RconPacket(0, RconPacket.Type.CommandRequest, message);
        
        // Telnet directly writes just the message to the stream 
        _streamWriter.WriteLine(message);
        
        return packet;
    }

    protected override RconPacket ReadPacket(int timeoutSeconds)
    {
        var response = _streamReader.ReadLine();
        if (response == null) // response will be null when we lost connection to the server
        {
            _stop = true;
            var reconnectSuccess = Reconnect();
            if (reconnectSuccess)
            {
                _streamReader = new StreamReader(Stream, Encoding.ASCII);
                _streamWriter = new StreamWriter(Stream, Encoding.ASCII) { AutoFlush = true };
                Auth();
                _stop = false;
                new Thread(() =>
                {
                    try
                    {
                        while (!_stop)
                        {
                            var packet = ReadPacket(-1);
                            Adapter.LogInfo(packet.ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        //pass if the poller fails to read a packet, usually during reconnect
                    }
                }).Start();
            }
            else
            {
                Adapter.LogError("Failed to reconnect to server.");
                Environment.Exit(1);
            }
        }
        
        return new RconPacket(0, (int)RconPacket.Type.CommandResponse, response);
    }

    public override bool Polls()
    {
        return true;
    }
}