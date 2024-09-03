using System.Net;
using System.Text;
using BattleNET;
using Tebex.Adapters;

namespace Tebex.RCON.Protocol;

public class BattleNetRcon : RconConnection
{
    private BattlEyeClient? _battlEye;
    
    public BattleNetRcon(TebexRconAdapter adapter, string host, int port, string password) : base(adapter, host, port, password)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public override Tuple<bool, string> Connect()
    {
        var rconLoginCreds = new BattlEyeLoginCredentials(IPAddress.Parse(_host), _port, _password);
        _battlEye = new BattlEyeClient(rconLoginCreds);
        _battlEye.ReconnectOnPacketLoss = true;
        _battlEye.BattlEyeMessageReceived += _BEMessageReceived;
        var result = _battlEye.Connect();

        switch (result)
        {
            case BattlEyeConnectionResult.ConnectionFailed:
                return new Tuple<bool, string>(false, "Connection failed. Please check your IP and port and try again.");
            case BattlEyeConnectionResult.InvalidLogin: 
                return new Tuple<bool, string>(false, "Invalid login. Please check your password and try again.");
            default:
                return new Tuple<bool, string>(true, "");
        }
    }

    protected override RconPacket SendPacket(RconPacket.Type requestType, string message)
    {
        var packet = new RconPacket(0, RconPacket.Type.CommandRequest, message);
        _battlEye.SendCommand(BattlEyeCommand.RConPassword, message);
        return packet;
    }

    protected override RconPacket ReadPacket(int timeoutSeconds)
    {
        // BattlEye provides responses via event BEMessageReceived 
        return new RconPacket(0, (int)RconPacket.Type.CommandResponse, ""); 
    }

    private void _BEMessageReceived(BattlEyeMessageEventArgs args)
    {
        var packet = new RconPacket(args.Id, RconPacket.Type.CommandResponse, args.Message);
        responses.Add(args.Id, packet);
    }
}