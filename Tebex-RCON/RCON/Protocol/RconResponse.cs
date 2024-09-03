namespace Tebex.RCON.Protocol;

public class RconResponse
{
    private RconPacket _request;
    private RconPacket _response;

    public RconResponse()
    {
        _request = null;
        _response = null;
    }
    
    public RconResponse(RconPacket request, RconPacket response)
    {
        this._request = request;
        this._response = response;
    }
    
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