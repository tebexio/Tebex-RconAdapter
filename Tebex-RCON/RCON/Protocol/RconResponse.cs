namespace Tebex.RCON.Protocol;

public class RconResponse
{
    private RconPacket _request;
    private RconPacket _response;

    public RconPacket Request
    {
        get => _request;
        set => _request = value;
    }
    public RconPacket Response
    {
        get => _response;
        set => _response = value;
    }
}