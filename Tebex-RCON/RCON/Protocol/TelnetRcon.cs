using System.Text;
using Tebex.Adapters;

namespace Tebex.RCON.Protocol;

public class TelnetRcon : RconConnection
{
    private StreamReader _streamReader;
    private StreamWriter _streamWriter;
    
    public TelnetRcon(TebexRconAdapter adapter, string host, int port, string password) : base(adapter, host, port, password)
    {
    }

    public override Tuple<bool, string> Connect()
    {
        var result = base.Connect();
        if (result.Item1) // successful TCP connection
        {
            _streamReader = new StreamReader(stream, Encoding.ASCII);
            _streamWriter = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
        }
        return base.Connect();
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
            response = "";
        }
        
        return new RconPacket(0, (int)RconPacket.Type.CommandResponse, response);
    }
}