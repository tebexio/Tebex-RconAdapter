using System.Net.Sockets;
using System.Text;
using Tebex.Adapters;

namespace Tebex.RCON.Protocol;

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
        client = new TcpClient();
        try
        {
            client.Connect(_host, _port);
            stream = client.GetStream();
            success = true;
        }
        catch (Exception e)
        {
            error = e.Message;
        }

        if (success) // successful TCP connection
        {
            _streamReader = new StreamReader(stream, Encoding.ASCII);
            _streamWriter = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
            var authResult = Auth(); // authorize after setting up our readers
            if (authResult.Item1) // successful auth
            {
                new Thread(() =>
                {
                    try
                    {
                        while (!_stop)
                        {
                            var packet = ReadPacket(-1);
                            _adapter.LogInfo(packet.ToString());
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

        return new Tuple<bool, string>(success, error);
    }

    public override Tuple<bool, string> Auth()
    {
        // expect "Please enter password"
        var message = ReceiveNext().Message;
        if (message.Contains("enter password"))
        {
            if (TebexRconAdapter.PluginConfig.RconPassword == "")
            {
                return new Tuple<bool, string>(false, "This server requires a password. Please configure your password in tebex-config.json or run `tebex.setup`");
            }
                
            // we will be prompted for a password first if enabled
            Send(_password);
                
            // expect "Logon successful"
            var passwordResponse = ReceiveNext();
            if (!passwordResponse.Message.Contains("successful"))
            {
                return new Tuple<bool, string>(false, "The server did not accept our password. Please try again.");
            }
        }

        return new Tuple<bool, string>(true, "");
    }

    protected override RconPacket SendPacket(RconPacket.Type requestType, string message)
    {
        var packet = new RconPacket(0, RconPacket.Type.CommandRequest, message);
        _streamWriter.WriteLine(message);
        return packet;
    }

    protected override RconPacket ReadPacket(int timeoutSeconds)
    {
        var response = _streamReader.ReadLine();
        if (response == null)
        {
            _stop = true;
            var reconnectSuccess = Reconnect();
            if (reconnectSuccess)
            {
                _streamReader = new StreamReader(stream, Encoding.ASCII);
                _streamWriter = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                Auth();
                _stop = false;
                new Thread(() =>
                {
                    try
                    {
                        while (!_stop)
                        {
                            var packet = ReadPacket(-1);
                            _adapter.LogInfo(packet.ToString());
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
                _adapter.LogError("Failed to reconnect to server.");
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